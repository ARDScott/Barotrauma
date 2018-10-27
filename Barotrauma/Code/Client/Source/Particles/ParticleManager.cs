﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma.Particles
{
    enum ParticleBlendState
    {
        AlphaBlend, Additive//, Distortion
    }

    class ParticleManager
    {
        private const int MaxOutOfViewDist = 500;

        private int particleCount;
        public int ParticleCount
        {
            get { return particleCount; }
        }

        private int maxParticles;
        public int MaxParticles
        {
            get { return maxParticles; }
            set
            {
                if (maxParticles == value || value < 4) return;

                Particle[] newParticles = new Particle[value];

                for (int i = 0; i < Math.Min(maxParticles, value); i++)
                {
                    newParticles[i] = particles[i];
                }

                particleCount = Math.Min(particleCount, value);
                particles = newParticles;
                maxParticles = value;
            }
        }
        private Particle[] particles;

        private Dictionary<string, ParticlePrefab> prefabs;

        private Camera cam;
        
        public ParticleManager(Camera cam)
        {
            this.cam = cam;

            MaxParticles = GameMain.Config.ParticleLimit;
        }

        public void LoadPrefabs()
        {
            prefabs = new Dictionary<string, ParticlePrefab>();
            foreach (string configFile in GameMain.Instance.GetFilesOfType(ContentType.Particles))
            {
                XDocument doc = XMLExtensions.TryLoadXml(configFile);
                if (doc == null || doc.Root == null) continue;

                foreach (XElement element in doc.Root.Elements())
                {
                    if (prefabs.ContainsKey(element.Name.ToString()))
                    {
                        DebugConsole.ThrowError("Error in " + configFile + "! Each particle prefab must have a unique name.");
                        continue;
                    }
                    prefabs.Add(element.Name.ToString(), new ParticlePrefab(element));
                }
            }         
        }

        public Particle CreateParticle(string prefabName, Vector2 position, float angle, float speed, Hull hullGuess = null)
        {
            return CreateParticle(prefabName, position, new Vector2((float)Math.Cos(angle), (float)-Math.Sin(angle)) * speed, angle, hullGuess);
        }

        public Particle CreateParticle(string prefabName, Vector2 position, Vector2 velocity, float rotation=0.0f, Hull hullGuess = null)
        {
            ParticlePrefab prefab = FindPrefab(prefabName);

            if (prefab == null)
            {
                DebugConsole.ThrowError("Particle prefab \"" + prefabName+"\" not found!");
                return null;
            }

            return CreateParticle(prefab, position, velocity, rotation, hullGuess);
        }

        public Particle CreateParticle(ParticlePrefab prefab, Vector2 position, Vector2 velocity, float rotation = 0.0f, Hull hullGuess = null)
        {
            if (particleCount >= MaxParticles || prefab == null) return null;

            Vector2 particleEndPos = prefab.CalculateEndPosition(position, velocity);

            Vector2 minPos = new Vector2(Math.Min(position.X, particleEndPos.X), Math.Min(position.Y, particleEndPos.Y));
            Vector2 maxPos = new Vector2(Math.Max(position.X, particleEndPos.X), Math.Max(position.Y, particleEndPos.Y));

            Rectangle expandedViewRect = MathUtils.ExpandRect(cam.WorldView, MaxOutOfViewDist);

            if (minPos.X > expandedViewRect.Right || maxPos.X < expandedViewRect.X) return null;
            if (minPos.Y > expandedViewRect.Y || maxPos.Y < expandedViewRect.Y - expandedViewRect.Height) return null;

            if (particles[particleCount] == null) particles[particleCount] = new Particle();

            particles[particleCount].Init(prefab, position, velocity, rotation, hullGuess);

            particleCount++;

            return particles[particleCount - 1];
        }

        public List<ParticlePrefab> GetPrefabList()
        {
            return prefabs.Values.ToList();
        }

        public ParticlePrefab FindPrefab(string prefabName)
        {
            ParticlePrefab prefab;
            prefabs.TryGetValue(prefabName, out prefab);

            if (prefab == null)
            {
                DebugConsole.ThrowError("Particle prefab " + prefabName + " not found!");
                return null;
            }

            return prefab;
        }

        private void RemoveParticle(int index)
        {
            particleCount--;

            Particle swap = particles[index];
            particles[index] = particles[particleCount];
            particles[particleCount] = swap;
        }

        public void Update(float deltaTime)
        {
            MaxParticles = GameMain.Config.ParticleLimit;

            for (int i = 0; i < particleCount; i++)
            {
                bool remove = false;
                try
                {
                    remove = !particles[i].Update(deltaTime);
                }
                catch (Exception e)
                {
                    DebugConsole.ThrowError("Particle update failed", e);
                    remove = true;
                }

                if (remove) RemoveParticle(i);
            }
        }

        public void UpdateTransforms()
        {
            for (int i = 0; i < particleCount; i++)
            {
                particles[i].UpdateDrawPos();
            }
        }

        public Dictionary<ParticlePrefab, int> CountActiveParticles()
        {
            Dictionary<ParticlePrefab, int> activeParticles = new Dictionary<ParticlePrefab, int>();
            for (int i = 0; i < particleCount; i++)
            {
                if (!activeParticles.ContainsKey(particles[i].Prefab)) activeParticles[particles[i].Prefab] = 0;
                activeParticles[particles[i].Prefab]++;
            }
            return activeParticles;
        }

        public void Draw(SpriteBatch spriteBatch, bool inWater, bool? inSub, ParticleBlendState blendState)
        {
            ParticlePrefab.DrawTargetType drawTarget = inWater ? ParticlePrefab.DrawTargetType.Water : ParticlePrefab.DrawTargetType.Air;

            for (int i = 0; i < particleCount; i++)
            {
                if (particles[i].BlendState != blendState) continue;
                //equivalent to !particles[i].DrawTarget.HasFlag(drawTarget) but garbage free and faster
                if ((particles[i].DrawTarget & drawTarget) == 0) continue;
                if (inSub.HasValue && (particles[i].CurrentHull == null) == inSub.Value) continue;
                
                particles[i].Draw(spriteBatch);
            }
        }

    }
}
