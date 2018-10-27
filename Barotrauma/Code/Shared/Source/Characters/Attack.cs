﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;


namespace Barotrauma
{    
    public enum HitDetection
    {
        Distance,
        Contact
    }

    struct AttackResult
    {
        public readonly float Damage;
        public readonly List<Affliction> Afflictions;

        public readonly Limb HitLimb;

        public readonly List<DamageModifier> AppliedDamageModifiers;
        
        public AttackResult(List<Affliction> afflictions, Limb hitLimb, List<DamageModifier> appliedDamageModifiers = null)
        {
            HitLimb = hitLimb;
            Afflictions = new List<Affliction>();

            foreach (Affliction affliction in afflictions)
            {
                Afflictions.Add(affliction.Prefab.Instantiate(affliction.Strength));
            }
            AppliedDamageModifiers = appliedDamageModifiers;
            Damage = Afflictions.Sum(a => a.GetVitalityDecrease(null));
        }

        public AttackResult(float damage, List<DamageModifier> appliedDamageModifiers = null)
        {
            Damage = damage;
            HitLimb = null;

            Afflictions = null;

            AppliedDamageModifiers = appliedDamageModifiers;
        }
    }
    
    partial class Attack
    {
        [Serialize(HitDetection.Distance, false)]
        public HitDetection HitDetectionType { get; private set; }

        [Serialize(0.0f, false)]
        public float Range { get; private set; }

        [Serialize(0.0f, false)]
        public float DamageRange { get; set; }

        [Serialize(0.0f, false)]
        public float Duration { get; private set; }
        
        [Serialize(0.0f, false)]
        public float StructureDamage { get; private set; }

        [Serialize(0.0f, false)]
        public float ItemDamage { get; private set; }

        [Serialize(0.0f, false)]
        public float Stun { get; private set; }

        [Serialize(false, false)]
        public bool OnlyHumans { get; private set; }

        //force applied to the attacking limb (or limbs defined using ApplyForceOnLimbs)
        //the direction of the force is towards the target that's being attacked
        [Serialize(0.0f, false)]
        public float Force { get; private set; }

        //torque applied to the attacking limb
        [Serialize(0.0f, false)]
        public float Torque { get; private set; }

        //impulse applied to the target the attack hits
        //the direction of the impulse is from this limb towards the target (use negative values to pull the target closer)
        [Serialize(0.0f, false)]
        public float TargetImpulse { get; private set; }

        //impulse applied to the target, in world space coordinates (i.e. 0,-1 pushes the target downwards)
        [Serialize("0.0, 0.0", false)]
        public Vector2 TargetImpulseWorld { get; private set; }

        //force applied to the target the attack hits 
        //the direction of the force is from this limb towards the target (use negative values to pull the target closer)
        [Serialize(0.0f, false)]
        public float TargetForce { get; private set; }

        //force applied to the target, in world space coordinates (i.e. 0,-1 pushes the target downwards)
        [Serialize("0.0, 0.0", false)]
        public Vector2 TargetForceWorld { get; private set; }

        [Serialize(0.0f, false)]
        public float SeverLimbsProbability { get; set; }

        [Serialize(0.0f, false)]
        public float Priority { get; private set; }

        public IEnumerable<StatusEffect> StatusEffects
        {
            get { return statusEffects; }
        }

        //the indices of the limbs Force is applied on 
        //(if none, force is applied only to the limb the attack is attached to)
        public readonly List<int> ApplyForceOnLimbs;

        public readonly List<Affliction> Afflictions = new List<Affliction>();

        private readonly List<StatusEffect> statusEffects;
        
        public List<Affliction> GetMultipliedAfflictions(float multiplier)
        {
            List<Affliction> multipliedAfflictions = new List<Affliction>();
            foreach (Affliction affliction in Afflictions)
            {
                multipliedAfflictions.Add(affliction.Prefab.Instantiate(affliction.Strength * multiplier));
            }
            return multipliedAfflictions;
        }

        public float GetStructureDamage(float deltaTime)
        {
            return (Duration == 0.0f) ? StructureDamage : StructureDamage * deltaTime;
        }

        public float GetItemDamage(float deltaTime)
        {
            return (Duration == 0.0f) ? ItemDamage : ItemDamage * deltaTime;
        }

        public float GetTotalDamage(bool includeStructureDamage = false)
        {
            float totalDamage = includeStructureDamage ? StructureDamage : 0.0f;
            foreach (Affliction affliction in Afflictions)
            {
                totalDamage += affliction.GetVitalityDecrease(null);
            }
            return totalDamage;
        }

        public Attack(float damage, float bleedingDamage, float burnDamage, float structureDamage, float range = 0.0f)
        {
            if (damage > 0.0f) Afflictions.Add(AfflictionPrefab.InternalDamage.Instantiate(damage));
            if (bleedingDamage > 0.0f) Afflictions.Add(AfflictionPrefab.Bleeding.Instantiate(bleedingDamage));
            if (burnDamage > 0.0f) Afflictions.Add(AfflictionPrefab.Burn.Instantiate(burnDamage));

            Range = range;
            DamageRange = range;
            StructureDamage = structureDamage;
        }

        public Attack(XElement element, string parentDebugName)
        {
            SerializableProperty.DeserializeProperties(this, element);
                                                            
            DamageRange = element.GetAttributeFloat("damagerange", Range);
            
            InitProjSpecific(element);

            string limbIndicesStr = element.GetAttributeString("applyforceonlimbs", "");
            if (!string.IsNullOrWhiteSpace(limbIndicesStr))
            {
                ApplyForceOnLimbs = new List<int>();
                foreach (string limbIndexStr in limbIndicesStr.Split(','))
                {
                    int limbIndex;
                    if (int.TryParse(limbIndexStr, out limbIndex))
                    {
                        ApplyForceOnLimbs.Add(limbIndex);
                    }
                }
            }

            foreach (XElement subElement in element.Elements())
            {
                switch (subElement.Name.ToString().ToLowerInvariant())
                {
                    case "statuseffect":
                        if (statusEffects == null)
                        {
                            statusEffects = new List<StatusEffect>();
                        }
                        statusEffects.Add(StatusEffect.Load(subElement, parentDebugName));
                        break;
                    case "affliction":
                        AfflictionPrefab afflictionPrefab;
                        if (subElement.Attribute("name") != null)
                        {
                            DebugConsole.ThrowError("Error in Attack (" + parentDebugName + ") - define afflictions using identifiers instead of names.");
                            string afflictionName = subElement.GetAttributeString("name", "").ToLowerInvariant();
                            afflictionPrefab = AfflictionPrefab.List.Find(ap => ap.Name.ToLowerInvariant() == afflictionName);
                            if (afflictionPrefab == null)
                            {
                                DebugConsole.ThrowError("Error in Attack (" + parentDebugName + ") - Affliction prefab \"" + afflictionName + "\" not found.");
                            }
                        }
                        else
                        {
                            string afflictionIdentifier = subElement.GetAttributeString("identifier", "").ToLowerInvariant();
                            afflictionPrefab = AfflictionPrefab.List.Find(ap => ap.Identifier.ToLowerInvariant() == afflictionIdentifier);
                            if (afflictionPrefab == null)
                            {
                                DebugConsole.ThrowError("Error in Attack (" + parentDebugName + ") - Affliction prefab \"" + afflictionIdentifier + "\" not found.");
                            }
                        }

                        float afflictionStrength = subElement.GetAttributeFloat(1.0f, "amount", "strength");
                        Afflictions.Add(afflictionPrefab.Instantiate(afflictionStrength));
                        break;
                }

            }
        }
        partial void InitProjSpecific(XElement element);
        
        public AttackResult DoDamage(Character attacker, IDamageable target, Vector2 worldPosition, float deltaTime, bool playSound = true)
        {
            Character targetCharacter = target as Character;
            if (OnlyHumans)
            {
                if (targetCharacter != null && targetCharacter.ConfigPath != Character.HumanConfigFile) return new AttackResult();
            }

            DamageParticles(deltaTime, worldPosition);
            
            var attackResult = target.AddDamage(attacker, worldPosition, this, deltaTime, playSound);
            var effectType = attackResult.Damage > 0.0f ? ActionType.OnUse : ActionType.OnFailure;
            if (targetCharacter != null && targetCharacter.IsDead)
            {
                effectType = ActionType.OnEating;
            }
            if (statusEffects == null) return attackResult;

            foreach (StatusEffect effect in statusEffects)
            {
                if (effect.HasTargetType(StatusEffect.TargetType.This))
                {
                    effect.Apply(effectType, deltaTime, attacker, attacker);
                }
                if (target is Character)
                {
                    if (effect.HasTargetType(StatusEffect.TargetType.Character))
                    {
                        effect.Apply(effectType, deltaTime, (Character)target, (Character)target);
                    }
                    if (effect.HasTargetType(StatusEffect.TargetType.Limb))
                    {
                        effect.Apply(effectType, deltaTime, (Character)target, attackResult.HitLimb);
                    }                    
                    if (effect.HasTargetType(StatusEffect.TargetType.AllLimbs))
                    {
                        effect.Apply(effectType, deltaTime, (Character)target, ((Character)target).AnimController.Limbs.Cast<ISerializableEntity>().ToList());
                    }
                }
            }

            return attackResult;
        }

        public AttackResult DoDamageToLimb(Character attacker, Limb targetLimb, Vector2 worldPosition, float deltaTime, bool playSound = true)
        {
            if (targetLimb == null) return new AttackResult();

            if (OnlyHumans)
            {
                if (targetLimb.character != null && targetLimb.character.ConfigPath != Character.HumanConfigFile) return new AttackResult();
            }

            DamageParticles(deltaTime, worldPosition);

            var attackResult = targetLimb.character.ApplyAttack(attacker, worldPosition, this, deltaTime, playSound, targetLimb);
            var effectType = attackResult.Damage > 0.0f ? ActionType.OnUse : ActionType.OnFailure;
            if (statusEffects == null) return attackResult;            

            foreach (StatusEffect effect in statusEffects)
            {
                if (effect.HasTargetType(StatusEffect.TargetType.This))
                {
                    effect.Apply(effectType, deltaTime, attacker, attacker);
                }
                if (effect.HasTargetType(StatusEffect.TargetType.Character))
                {
                    effect.Apply(effectType, deltaTime, targetLimb.character, targetLimb.character);
                }
                if (effect.HasTargetType(StatusEffect.TargetType.Limb))
                {
                    effect.Apply(effectType, deltaTime, targetLimb.character, targetLimb);
                }
                if (effect.HasTargetType(StatusEffect.TargetType.AllLimbs))
                {
                    effect.Apply(effectType, deltaTime, targetLimb.character, targetLimb.character.AnimController.Limbs.Cast<ISerializableEntity>().ToList());
                }

            }

            return attackResult;
        }

        partial void DamageParticles(float deltaTime, Vector2 worldPosition);
    }
}
