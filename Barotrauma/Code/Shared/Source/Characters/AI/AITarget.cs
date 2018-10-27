﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Barotrauma
{
    partial class AITarget
    {
        public static List<AITarget> List = new List<AITarget>();

        public Entity Entity
        {
            get;
            private set;
        }

        private float soundRange;
        private float sightRange;
        
        public float SoundRange
        {
            get { return soundRange; }
            set { soundRange = Math.Max(value, MinSoundRange); }
        }

        public float SightRange
        {
            get { return sightRange; }
            set { sightRange = Math.Max(value, MinSightRange); }
        }

        private float sectorRad = MathHelper.TwoPi;
        public float SectorDegrees
        {
            get { return MathHelper.ToDegrees(sectorRad); }
            set { sectorRad = MathHelper.ToRadians(value); }
        }

        private Vector2 sectorDir;
        public Vector2 SectorDir
        {
            get { return sectorDir; }
            set
            {
                if (!MathUtils.IsValid(value))
                {
                    string errorMsg = "Invalid AITarget sector direction (" + value + ")\n" + Environment.StackTrace;
                    DebugConsole.ThrowError(errorMsg);
                    GameAnalyticsManager.AddErrorEventOnce("AITarget.SectorDir:" + Entity?.ToString(), GameAnalyticsSDK.Net.EGAErrorSeverity.Error, errorMsg);
                    return;
                }
                sectorDir = value;
            }
        }

        public string SonarLabel;

        public bool Enabled = true;

        public float MinSoundRange, MinSightRange;

        public Vector2 WorldPosition
        {
            get
            {
                if (Entity == null || Entity.Removed)
                {
#if DEBUG
                    DebugConsole.ThrowError("Attempted to access a removed AITarget\n" + Environment.StackTrace);
#endif
                    GameAnalyticsManager.AddErrorEventOnce("AITarget.WorldPosition:EntityRemoved",
                        GameAnalyticsSDK.Net.EGAErrorSeverity.Error,
                        "Attempted to access a removed AITarget\n" + Environment.StackTrace);
                    return Vector2.Zero;
                }

                return Entity.WorldPosition;
            }
        }

        public Vector2 SimPosition
        {
            get
            {
                if (Entity == null || Entity.Removed)
                {
#if DEBUG
                    DebugConsole.ThrowError("Attempted to access a removed AITarget\n" + Environment.StackTrace);
#endif
                    GameAnalyticsManager.AddErrorEventOnce("AITarget.WorldPosition:EntityRemoved",
                        GameAnalyticsSDK.Net.EGAErrorSeverity.Error,
                        "Attempted to access a removed AITarget\n" + Environment.StackTrace);
                    return Vector2.Zero;
                }

                return Entity.SimPosition;
            }
        }

        public AITarget(Entity e, XElement element) : this(e)
        {
            SightRange = MinSightRange = element.GetAttributeFloat("sightrange", 0.0f);
            SoundRange = MinSoundRange = element.GetAttributeFloat("soundrange", 0.0f);
            SonarLabel = element.GetAttributeString("sonarlabel", "");
        }

        public AITarget(Entity e)
        {
            Entity = e;
            List.Add(this);
        }

        public bool IsWithinSector(Vector2 worldPosition)
        {
            if (sectorRad >= MathHelper.TwoPi) return true;

            Vector2 diff = worldPosition - WorldPosition;
            return MathUtils.GetShortestAngle(MathUtils.VectorToAngle(diff), MathUtils.VectorToAngle(sectorDir)) <= sectorRad * 0.5f;
        }

        public void Remove()
        {
            List.Remove(this);
            Entity = null;
        }
    }
}
