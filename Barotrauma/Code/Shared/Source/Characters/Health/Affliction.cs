﻿using Microsoft.Xna.Framework;
using System;
using System.Linq;

namespace Barotrauma
{
    class Affliction
    {
        public readonly AfflictionPrefab Prefab;
        
        public float Strength;

        public float DamagePerSecond;
        public float DamagePerSecondTimer;
        public float PreviousVitalityDecrease;

        public Affliction(AfflictionPrefab prefab, float strength)
        {
            Prefab = prefab;
            Strength = strength;
        }

        public Affliction CreateMultiplied(float multiplier)
        {
            return Prefab.Instantiate(Strength * multiplier);
        }

        public override string ToString()
        {
            return "Affliction (" + Prefab.Name + ")";
        }

        public float GetVitalityDecrease(CharacterHealth characterHealth)
        {
            if (Strength < Prefab.ActivationThreshold) return 0.0f;
            AfflictionPrefab.Effect currentEffect = Prefab.GetActiveEffect(Strength);
            if (currentEffect == null) return 0.0f;
            if (currentEffect.MaxStrength - currentEffect.MinStrength <= 0.0f) return 0.0f;

            float currVitalityDecrease = MathHelper.Lerp(
                currentEffect.MinVitalityDecrease, 
                currentEffect.MaxVitalityDecrease, 
                (Strength - currentEffect.MinStrength) / (currentEffect.MaxStrength - currentEffect.MinStrength));

            if (currentEffect.MultiplyByMaxVitality) currVitalityDecrease *= characterHealth == null ? 100.0f : characterHealth.MaxVitality;

            return currVitalityDecrease;
        }

        public float GetScreenDistortStrength()
        {
            if (Strength < Prefab.ActivationThreshold) return 0.0f;
            AfflictionPrefab.Effect currentEffect = Prefab.GetActiveEffect(Strength);
            if (currentEffect == null) return 0.0f;
            if (currentEffect.MaxScreenDistortStrength - currentEffect.MinScreenDistortStrength <= 0.0f) return 0.0f;

            return MathHelper.Lerp(
                currentEffect.MinScreenDistortStrength,
                currentEffect.MaxScreenDistortStrength,
                (Strength - currentEffect.MinStrength) / (currentEffect.MaxStrength - currentEffect.MinStrength));
        }

        public float GetRadialDistortStrength()
        {
            if (Strength < Prefab.ActivationThreshold) return 0.0f;
            AfflictionPrefab.Effect currentEffect = Prefab.GetActiveEffect(Strength);
            if (currentEffect == null) return 0.0f;
            if (currentEffect.MaxRadialDistortStrength - currentEffect.MinRadialDistortStrength <= 0.0f) return 0.0f;

            return MathHelper.Lerp(
                currentEffect.MinRadialDistortStrength,
                currentEffect.MaxRadialDistortStrength,
                (Strength - currentEffect.MinStrength) / (currentEffect.MaxStrength - currentEffect.MinStrength));
        }

        public float GetChromaticAberrationStrength()
        {
            if (Strength < Prefab.ActivationThreshold) return 0.0f;
            AfflictionPrefab.Effect currentEffect = Prefab.GetActiveEffect(Strength);
            if (currentEffect == null) return 0.0f;
            if (currentEffect.MaxChromaticAberrationStrength - currentEffect.MinChromaticAberrationStrength <= 0.0f) return 0.0f;

            return MathHelper.Lerp(
                currentEffect.MinChromaticAberrationStrength,
                currentEffect.MaxChromaticAberrationStrength,
                (Strength - currentEffect.MinStrength) / (currentEffect.MaxStrength - currentEffect.MinStrength));
        }

        public float GetScreenBlurStrength()
        {
            if (Strength < Prefab.ActivationThreshold) return 0.0f;
            AfflictionPrefab.Effect currentEffect = Prefab.GetActiveEffect(Strength);
            if (currentEffect == null) return 0.0f;
            if (currentEffect.MaxScreenBlurStrength - currentEffect.MinScreenBlurStrength <= 0.0f) return 0.0f;

            return MathHelper.Lerp(
                currentEffect.MinScreenBlurStrength,
                currentEffect.MaxScreenBlurStrength,
                (Strength - currentEffect.MinStrength) / (currentEffect.MaxStrength - currentEffect.MinStrength));
        }

        public void CalculateDamagePerSecond(float currentVitalityDecrease)
        {
            DamagePerSecond = Math.Max(DamagePerSecond, currentVitalityDecrease - PreviousVitalityDecrease);
            if (DamagePerSecondTimer >= 1.0f)
            {
                DamagePerSecond = currentVitalityDecrease - PreviousVitalityDecrease;
                PreviousVitalityDecrease = currentVitalityDecrease;
                DamagePerSecondTimer = 0.0f;
            }
        }

        public virtual void Update(CharacterHealth characterHealth, Limb targetLimb, float deltaTime)
        {
            AfflictionPrefab.Effect currentEffect = Prefab.GetActiveEffect(Strength);
            if (currentEffect == null) return;

            Strength += currentEffect.StrengthChange * deltaTime;
            foreach (StatusEffect statusEffect in currentEffect.StatusEffects)
            {
                if (statusEffect.HasTargetType(StatusEffect.TargetType.Character))
                {
                    statusEffect.Apply(ActionType.OnActive, deltaTime, characterHealth.Character, characterHealth.Character);
                }
                if (targetLimb != null && statusEffect.HasTargetType(StatusEffect.TargetType.Limb))
                {
                    statusEffect.Apply(ActionType.OnActive, deltaTime, characterHealth.Character, targetLimb);
                }
                if (targetLimb != null && statusEffect.HasTargetType(StatusEffect.TargetType.AllLimbs))
                {
                    statusEffect.Apply(ActionType.OnActive, deltaTime, targetLimb.character, targetLimb.character.AnimController.Limbs.Cast<ISerializableEntity>().ToList());
                }
            }
        }
    }
}
