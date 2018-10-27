﻿using Barotrauma.Items.Components;
using Barotrauma.Networking;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma
{
    partial class Explosion
    {
        private static List<Triplet<Explosion, Vector2, float>> prevExplosions = new List<Triplet<Explosion, Vector2, float>>();

        private Attack attack;
        
        private float force;
        
        public float CameraShake;

        private bool sparks, shockwave, flames, smoke, flash, underwaterBubble;

        private float empStrength;

        private string decal;
        private float decalSize;

        public Explosion(float range, float force, float damage, float structureDamage, float empStrength = 0.0f)
        {
            attack = new Attack(damage, 0.0f, 0.0f, structureDamage, range);
            attack.SeverLimbsProbability = 1.0f;
            this.force = force;
            this.empStrength = empStrength;
            sparks = true;
            shockwave = true;
            smoke = true;
            flames = true;
            underwaterBubble = true;
        }

        public Explosion(XElement element, string parentDebugName)
        {
            attack = new Attack(element, parentDebugName + ", Explosion");

            force = element.GetAttributeFloat("force", 0.0f);

            sparks      = element.GetAttributeBool("sparks", true);
            shockwave   = element.GetAttributeBool("shockwave", true);
            flames      = element.GetAttributeBool("flames", true);
            underwaterBubble = element.GetAttributeBool("underwaterbubble", true);
            smoke       = element.GetAttributeBool("smoke", true);
            flash       = element.GetAttributeBool("flash", true);

            empStrength = element.GetAttributeFloat("empstrength", 0.0f);

            decal       = element.GetAttributeString("decal", "");
            decalSize   = element.GetAttributeFloat("decalSize", 1.0f);

            CameraShake = element.GetAttributeFloat("camerashake", attack.Range * 0.1f);
        }

        public List<Triplet<Explosion, Vector2, float>> GetRecentExplosions(float maxSecondsAgo)
        {
            return prevExplosions.FindAll(e => e.Third >= Timing.TotalTime - maxSecondsAgo);
        }
        
        public void Explode(Vector2 worldPosition, Entity damageSource)
        {
            prevExplosions.Add(new Triplet<Explosion, Vector2, float>(this, worldPosition, (float)Timing.TotalTime));
            if (prevExplosions.Count > 100)
            {
                prevExplosions.RemoveAt(0);
            }

            Hull hull = Hull.FindHull(worldPosition);

            ExplodeProjSpecific(worldPosition, hull);

            float displayRange = attack.Range;
            if (displayRange < 0.1f) return;

            Vector2 cameraPos = Character.Controlled != null ? Character.Controlled.WorldPosition : GameMain.GameScreen.Cam.Position;
            float cameraDist = Vector2.Distance(cameraPos, worldPosition) / 2.0f;
            GameMain.GameScreen.Cam.Shake = CameraShake * Math.Max((displayRange - cameraDist) / displayRange, 0.0f);
            
            if (attack.GetStructureDamage(1.0f) > 0.0f)
            {
                RangedStructureDamage(worldPosition, displayRange, attack.GetStructureDamage(1.0f));
            }

            if (empStrength > 0.0f)
            {
                float displayRangeSqr = displayRange * displayRange;
                foreach (Item item in Item.ItemList)
                {
                    float distSqr = Vector2.DistanceSquared(item.WorldPosition, worldPosition);
                    if (distSqr > displayRangeSqr) continue;
                    
                    float distFactor = 1.0f - (float)Math.Sqrt(distSqr) / displayRange;

                    //damage repairable power-consuming items
                    var powered = item.GetComponent<Powered>();
                    if (powered == null || !powered.VulnerableToEMP) continue;
                    if (item.Repairables.Any())
                    {
                        item.Condition -= 100 * empStrength * distFactor;
                    }

                    //discharge batteries
                    var powerContainer = item.GetComponent<PowerContainer>();
                    if (powerContainer != null)
                    {
                        powerContainer.Charge -= powerContainer.Capacity * empStrength * distFactor;
                    }                    
                }
            }

            if (force == 0.0f && attack.Stun == 0.0f && attack.GetTotalDamage(false) == 0.0f) return;

            DamageCharacters(worldPosition, attack, force, damageSource);

            if (flames && GameMain.Client == null)
            {
                foreach (Item item in Item.ItemList)
                {
                    if (item.CurrentHull != hull || item.FireProof || item.Condition <= 0.0f) continue;

                    //don't apply OnFire effects if the item is inside a fireproof container
                    //(or if it's inside a container that's inside a fireproof container, etc)
                    Item container = item.Container;
                    while (container != null)
                    {
                        if (container.FireProof) return;
                        container = container.Container;
                    }

                    if (Vector2.Distance(item.WorldPosition, worldPosition) > attack.Range * 0.1f) continue;

                    item.ApplyStatusEffects(ActionType.OnFire, 1.0f);

                    if (item.Condition <= 0.0f && GameMain.Server != null)
                    {
                        GameMain.Server.CreateEntityEvent(item, new object[] { NetEntityEvent.Type.ApplyStatusEffect, ActionType.OnFire });
                    }
                }
            }

        }

        partial void ExplodeProjSpecific(Vector2 worldPosition, Hull hull);

        private Vector2 ClampParticlePos(Vector2 particlePos, Hull hull)
        {
            if (hull == null) return particlePos;

            return new Vector2(
                MathHelper.Clamp(particlePos.X, hull.WorldRect.X, hull.WorldRect.Right),
                MathHelper.Clamp(particlePos.Y, hull.WorldRect.Y - hull.WorldRect.Height, hull.WorldRect.Y));
        }

        public static void DamageCharacters(Vector2 worldPosition, Attack attack, float force, Entity damageSource)
        {
            if (attack.Range <= 0.0f) return;

            foreach (Character c in Character.CharacterList)
            {
                Vector2 explosionPos = worldPosition;
                if (c.Submarine != null) explosionPos -= c.Submarine.Position;

                Hull hull = Hull.FindHull(ConvertUnits.ToDisplayUnits(explosionPos), null, false);
                bool underWater = hull == null || explosionPos.Y < hull.Surface;

                explosionPos = ConvertUnits.ToSimUnits(explosionPos);

                Dictionary<Limb, float> distFactors = new Dictionary<Limb, float>();
                foreach (Limb limb in c.AnimController.Limbs)
                {
                    float dist = Vector2.Distance(limb.WorldPosition, worldPosition);
                    
                    //calculate distance from the "outer surface" of the physics body
                    //doesn't take the rotation of the limb into account, but should be accurate enough for this purpose
                    float limbRadius = Math.Max(Math.Max(limb.body.width * 0.5f, limb.body.height * 0.5f), limb.body.radius);
                    dist = Math.Max(0.0f, dist - FarseerPhysics.ConvertUnits.ToDisplayUnits(limbRadius));
                    
                    if (dist > attack.Range) continue;

                    float distFactor = 1.0f - dist / attack.Range;

                    //solid obstacles between the explosion and the limb reduce the effect of the explosion by 90%
                    if (Submarine.CheckVisibility(limb.SimPosition, explosionPos) != null) distFactor *= 0.1f;
                    
                    distFactors.Add(limb, distFactor);
                    
                    List<Affliction> modifiedAfflictions = new List<Affliction>();
                    foreach (Affliction affliction in attack.Afflictions)
                    {
                        modifiedAfflictions.Add(affliction.CreateMultiplied(distFactor));
                    }
                    c.LastDamageSource = damageSource;
                    Character attacker = null;
                    if (damageSource is Item item)
                    {
                        attacker = item.GetComponent<Projectile>()?.User;
                        if (attacker == null) attacker = item.GetComponent<MeleeWeapon>()?.User;
                    }
                    c.AddDamage(limb.WorldPosition, modifiedAfflictions, attack.Stun * distFactor, false, attacker: attacker);

                    if (attack.StatusEffects != null && attack.StatusEffects.Any())
                    {
                        var statusEffectTargets = new List<ISerializableEntity>() { c, limb };
                        foreach (StatusEffect statusEffect in attack.StatusEffects)
                        {
                            statusEffect.Apply(ActionType.OnUse, 1.0f, damageSource, statusEffectTargets);
                            statusEffect.Apply(ActionType.Always, 1.0f, damageSource, statusEffectTargets);
                            if (underWater) statusEffect.Apply(ActionType.InWater, 1.0f, damageSource, statusEffectTargets);
                        }
                    }
                    
                    if (limb.WorldPosition != worldPosition && force > 0.0f)
                    {
                        Vector2 limbDiff = Vector2.Normalize(limb.WorldPosition - worldPosition);
                        if (!MathUtils.IsValid(limbDiff)) limbDiff = Rand.Vector(1.0f);
                        Vector2 impulsePoint = limb.SimPosition - limbDiff * limbRadius;
                        limb.body.ApplyLinearImpulse(limbDiff * distFactor * force, impulsePoint);
                    }
                }     
                
                //sever joints 
                if (c.IsDead && attack.SeverLimbsProbability > 0.0f)
                {
                    foreach (Limb limb in c.AnimController.Limbs)
                    {
                        if (!distFactors.ContainsKey(limb)) continue;

                        foreach (LimbJoint joint in c.AnimController.LimbJoints)
                        {
                            if (joint.IsSevered || (joint.LimbA != limb && joint.LimbB != limb)) continue;

                            if (Rand.Range(0.0f, 1.0f) < attack.SeverLimbsProbability * distFactors[limb])
                            {
                                c.AnimController.SeverLimbJoint(joint);
                            }
                        }
                    }
                }          
            }
        }

        /// <summary>
        /// Returns a dictionary where the keys are the structures that took damage and the values are the amount of damage taken
        /// </summary>
        public static Dictionary<Structure, float> RangedStructureDamage(Vector2 worldPosition, float worldRange, float damage)
        {
            List<Structure> structureList = new List<Structure>();            
            float dist = 600.0f;
            foreach (MapEntity entity in MapEntity.mapEntityList)
            {
                Structure structure = entity as Structure;
                if (structure == null) continue;

                if (structure.HasBody &&
                    !structure.IsPlatform &&
                    Vector2.Distance(structure.WorldPosition, worldPosition) < dist * 3.0f)
                {
                    structureList.Add(structure);
                }
            }

            Dictionary<Structure, float> damagedStructures = new Dictionary<Structure, float>();
            foreach (Structure structure in structureList)
            {
                for (int i = 0; i < structure.SectionCount; i++)
                {
                    float distFactor = 1.0f - (Vector2.Distance(structure.SectionPosition(i, true), worldPosition) / worldRange);
                    if (distFactor <= 0.0f) continue;
                    
                    structure.AddDamage(i, damage * distFactor);

                    if (damagedStructures.ContainsKey(structure))
                    { 
                        damagedStructures[structure] += damage * distFactor;
                    }
                    else
                    {
                        damagedStructures.Add(structure, damage * distFactor);
                    }
                }                
            }

            return damagedStructures;
        }
    }
}
