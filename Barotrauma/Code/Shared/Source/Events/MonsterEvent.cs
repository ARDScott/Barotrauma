﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma
{
    class MonsterEvent : ScriptedEvent
    {
        private string characterFile;

        private int minAmount, maxAmount;

        private Character[] monsters;

        private bool spawnDeep;

        private Vector2 spawnPos;

        private bool disallowed;
                
        private Level.PositionType spawnPosType;

        private bool spawnPending;
        
        public override Vector2 DebugDrawPos
        {
            get { return spawnPos; }
        }

        public override string DebugDrawText
        {
            get { return "MonsterEvent (" + characterFile + ")"; }
        }

        public override string ToString()
        {
            return "ScriptedEvent (" + characterFile + ")";
        }

        private bool isActive;
        public override bool IsActive
        {
            get
            {
                return isActive;
            }
        }

        public MonsterEvent(ScriptedEventPrefab prefab)
            : base (prefab)
        {
            characterFile = prefab.ConfigElement.GetAttributeString("characterfile", "");

            int defaultAmount = prefab.ConfigElement.GetAttributeInt("amount", 1);
            minAmount = prefab.ConfigElement.GetAttributeInt("minamount", defaultAmount);
            maxAmount = Math.Max(prefab.ConfigElement.GetAttributeInt("maxamount", 1), minAmount);

            var spawnPosTypeStr = prefab.ConfigElement.GetAttributeString("spawntype", "");

            if (string.IsNullOrWhiteSpace(spawnPosTypeStr) ||
                !Enum.TryParse(spawnPosTypeStr, true, out spawnPosType))
            {
                spawnPosType = Level.PositionType.MainPath;
            }

            spawnDeep = prefab.ConfigElement.GetAttributeBool("spawndeep", false);

            if (GameMain.NetworkMember != null)
            {
                List<string> monsterNames = GameMain.NetworkMember.monsterEnabled.Keys.ToList();
                string characterName = Path.GetFileName(Path.GetDirectoryName(characterFile)).ToLower();
                string tryKey = monsterNames.Find(s => characterName == s.ToLower());
                if (!string.IsNullOrWhiteSpace(tryKey))
                {
                    if (!GameMain.NetworkMember.monsterEnabled[tryKey]) disallowed = true; //spawn was disallowed by host
                }
            }
        }

        public override bool CanAffectSubImmediately(Level level)
        {
            List<Vector2> positions = GetAvailableSpawnPositions();
            foreach (Vector2 position in positions)
            {
                if (Vector2.DistanceSquared(position, Submarine.MainSub.WorldPosition) < 10000.0f * 10000.0f)
                {
                    return true;
                }
            }

            return false;
        }
        
        public override void Init(bool affectSubImmediately)
        {
            FindSpawnPosition(affectSubImmediately);
            if (GameSettings.VerboseLogging)
            {
                DebugConsole.NewMessage("Initialized MonsterEvent (" + characterFile + ")", Color.White);
            }
        }

        private List<Vector2> GetAvailableSpawnPositions()
        {
            var availablePositions = Level.Loaded.PositionsOfInterest.FindAll(p => spawnPosType.HasFlag(p.PositionType));

            List<Vector2> positions = new List<Vector2>();
            foreach (var allowedPosition in availablePositions)
            {
                positions.Add(allowedPosition.Position);
            }

            if (spawnDeep)
            {
                for (int i = 0; i < positions.Count; i++)
                {
                    positions[i] = new Vector2(positions[i].X, positions[i].Y - Level.Loaded.Size.Y);
                }
            }

            positions.RemoveAll(pos => pos.Y < Level.Loaded.GetBottomPosition(pos.X).Y);
            
            return positions;
        }

        private void FindSpawnPosition(bool affectSubImmediately)
        {
            if (disallowed) return;

            spawnPos = Vector2.Zero;
            var availablePositions = GetAvailableSpawnPositions();
            if (affectSubImmediately)
            {
                if (availablePositions.Count == 0)
                {
                    //no suitable position found, disable the event
                    Finished();
                    return;
                }

                float closestDist = float.PositiveInfinity;
                //find the closest spawnposition that isn't too close to any of the subs
                foreach (Vector2 position in availablePositions)
                {
                    float dist = Vector2.DistanceSquared(position, Submarine.MainSub.WorldPosition);
                    foreach (Submarine sub in Submarine.Loaded)
                    {
                        float minDistToSub = GetMinDistanceToSub(sub);
                        if (dist > minDistToSub * minDistToSub && dist < closestDist)
                        {
                            closestDist = dist;
                            spawnPos = position;
                        }
                    }
                }

                //only found a spawnpos that's very far from the sub, pick one that's closer
                //and wait for the sub to move further before spawning
                if (closestDist > 10000.0f * 10000.0f)
                {
                    foreach (Vector2 position in availablePositions)
                    {
                        float dist = Vector2.DistanceSquared(position, Submarine.MainSub.WorldPosition);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            spawnPos = position;
                        }
                    }
                }
            }
            else
            {
                float minDist = spawnPosType == Level.PositionType.Ruin ? 0.0f : 20000.0f;
                availablePositions.RemoveAll(p => Vector2.Distance(Submarine.MainSub.WorldPosition, p) < minDist);
                if (availablePositions.Count == 0)
                {
                    //no suitable position found, disable the event
                    Finished();
                    return;
                }

                spawnPos = availablePositions[Rand.Int(availablePositions.Count, Rand.RandSync.Server)];
            }
            spawnPending = true;
        }

        private float GetMinDistanceToSub(Submarine submarine)
        {
            //5000 units is slightly more than the default range of the sonar
            return Math.Max(Math.Max(submarine.Borders.Width, submarine.Borders.Height), 5000.0f);
        }

        public override void Update(float deltaTime)
        {
            if (disallowed)
            {
                Finished();
                return;
            }
            
            if (isFinished) return;

            //isActive = false;

            if (spawnPending)
            {
                //wait until there are no submarines at the spawnpos
                foreach (Submarine submarine in Submarine.Loaded)
                {
                    float minDist = GetMinDistanceToSub(submarine);
                    if (Vector2.DistanceSquared(submarine.WorldPosition, spawnPos) < minDist * minDist) return;
                }

                //+1 because Range returns an integer less than the max value
                int amount = Rand.Range(minAmount, maxAmount + 1, Rand.RandSync.Server);
                monsters = new Character[amount];

                for (int i = 0; i < amount; i++)
                {
                    monsters[i] = Character.Create(
                        characterFile, spawnPos + Rand.Vector(100.0f, Rand.RandSync.Server), 
                        i.ToString(), null, GameMain.Client != null, true, true);
                }

                spawnPending = false;
            }

            Entity targetEntity = Character.Controlled != null ? 
                (Entity)Character.Controlled : Submarine.FindClosest(GameMain.GameScreen.Cam.WorldViewCenter);
            
            bool monstersDead = true;
            foreach (Character monster in monsters)
            {
                if (!monster.IsDead)
                {
                    monstersDead = false;

                    if (targetEntity != null && Vector2.DistanceSquared(monster.WorldPosition, targetEntity.WorldPosition) < 5000.0f * 5000.0f)
                    {
                        isActive = true;
                        break;
                    }
                }
            }

            if (monstersDead) Finished();
        }
    }
}
