﻿using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Barotrauma
{
    class FishWalkParams : FishGroundedParams
    {
        public static FishWalkParams GetDefaultAnimParams(Character character)
        {
            return Check(character) ? GetDefaultAnimParams<FishWalkParams>(character.SpeciesName, AnimationType.Walk) : Empty;
        }
        public static FishWalkParams GetAnimParams(Character character, string fileName = null)
        {
            return Check(character) ? GetAnimParams<FishWalkParams>(character.SpeciesName, AnimationType.Walk, fileName) : Empty;
        }

        protected static FishWalkParams Empty = new FishWalkParams();
    }

    class FishRunParams : FishGroundedParams
    {
        public static FishRunParams GetDefaultAnimParams(Character character)
        {
            return Check(character) ? GetDefaultAnimParams<FishRunParams>(character.SpeciesName, AnimationType.Run) : Empty;
        }
        public static FishRunParams GetAnimParams(Character character, string fileName = null)
        {
            return Check(character) ? GetAnimParams<FishRunParams>(character.SpeciesName, AnimationType.Run, fileName) : Empty;
        }

        protected static FishRunParams Empty = new FishRunParams();
    }

    class FishSwimFastParams : FishSwimParams
    {
        public static FishSwimFastParams GetDefaultAnimParams(Character character) => GetDefaultAnimParams<FishSwimFastParams>(character.SpeciesName, AnimationType.SwimFast);
        public static FishSwimFastParams GetAnimParams(Character character, string fileName = null)
        {
            return GetAnimParams<FishSwimFastParams>(character.SpeciesName, AnimationType.SwimFast, fileName);
        }
    }

    class FishSwimSlowParams : FishSwimParams
    {
        public static FishSwimSlowParams GetDefaultAnimParams(Character character) => GetDefaultAnimParams<FishSwimSlowParams>(character.SpeciesName, AnimationType.SwimSlow);
        public static FishSwimSlowParams GetAnimParams(Character character, string fileName = null)
        {
            return GetAnimParams<FishSwimSlowParams>(character.SpeciesName, AnimationType.SwimSlow, fileName);
        }
    }

    abstract class FishGroundedParams : GroundedMovementParams, IFishAnimation
    {
        protected static bool Check(Character character)
        {
            if (!character.AnimController.CanWalk)
            {
                DebugConsole.ThrowError($"{character.SpeciesName} cannot use run animations!");
                return false;
            }
            return true;
        }

        [Serialize(true, true), Editable(ToolTip = "Should the character be flipped depending on which direction it faces. Should usually be enabled on all characters that have distinctive upper and lower sides.")]
        public bool Flip { get; set; }

        [Serialize(10.0f, true), Editable(MinValueFloat = 0, MaxValueFloat = 50, ToolTip = "How much force is used to move the head to the correct position.")]
        public float HeadMoveForce { get; set; }

        [Serialize(10.0f, true), Editable(MinValueFloat = 0, MaxValueFloat = 50, ToolTip = "How much force is used to move the torso to the correct position.")]
        public float TorsoMoveForce { get; set; }

        [Serialize(8.0f, true), Editable(MinValueFloat = 0, MaxValueFloat = 50, ToolTip = "How much force is used to move the feet to the correct position.")]
        public float FootMoveForce { get; set; }

        [Serialize(50.0f, true), Editable(MinValueFloat = 0, MaxValueFloat = 200, ToolTip = "How much torque is used to rotate the head to the correct orientation.")]
        public float HeadTorque { get; set; }

        [Serialize(50.0f, true), Editable(MinValueFloat = 0, MaxValueFloat = 200, ToolTip = "How much torque is used to rotate the torso to the correct orientation.")]
        public float TorsoTorque { get; set; }

        [Serialize(25.0f, true), Editable(MinValueFloat = 0, MaxValueFloat = 500, ToolTip = "How much torque is used to rotate the feet to the correct orientation.")]
        public float FootTorque { get; set; }

        [Serialize(0.0f, true), Editable(MinValueFloat = 0, MaxValueFloat = 200, ToolTip = "Optional torque that's constantly applied to legs.")]
        public float LegTorque { get; set; }

        /// <summary>
        /// The angle of the collider when standing (i.e. out of water).
        /// In degrees.
        /// </summary>
        [Serialize(0f, true), Editable(MinValueFloat = -360, MaxValueFloat = 360, ToolTip = "The angle of the character's collider when standing.")]
        public float ColliderStandAngle
        {
            get => MathHelper.ToDegrees(ColliderStandAngleInRadians);
            set => ColliderStandAngleInRadians = MathHelper.ToRadians(value);
        }
        public float ColliderStandAngleInRadians { get; private set; }
        
        [Serialize(null, true), Editable]
        public string FootAngles
        {
            get => ParseFootAngles(FootAnglesInRadians);
            set => SetFootAngles(FootAnglesInRadians, value);
        }
        
        /// <summary>
        /// Key = limb id, value = angle in radians
        /// </summary>
        public Dictionary<int, float> FootAnglesInRadians { get; set; } = new Dictionary<int, float>();
    }

    abstract class FishSwimParams : SwimParams, IFishAnimation
    {
        [Serialize(true, true), Editable(ToolTip = "Should the character be flipped depending on which direction it faces. Should usually be enabled on all characters that have distinctive upper and lower sides.")]
        public bool Flip { get; set; }

        [Serialize(false, true), Editable(ToolTip = "If enabled, the character will simply be mirrored horizontally when it wants to turn around. If disabled, it will rotate itself to face the other direction.")]
        public bool Mirror { get; set; }

        [Serialize(1f, true), Editable]
        public float WaveAmplitude { get; set; }

        [Serialize(1f, true), Editable]
        public float WaveLength { get; set; }

        [Serialize(true, true), Editable(ToolTip = "Should the character face towards the direction it's heading.")]
        public bool RotateTowardsMovement { get; set; }

        [Serialize(25.0f, true), Editable(MinValueFloat = 0, MaxValueFloat = 500, ToolTip = "How much torque is used to rotate the torso to the correct orientation.")]
        public float TorsoTorque { get; set; }
        
        [Serialize(25.0f, true), Editable(MinValueFloat = 0, MaxValueFloat = 500, ToolTip = "How much torque is used to rotate the head to the correct orientation.")]
        public float HeadTorque { get; set; }

        [Serialize(25.0f, true), Editable(MinValueFloat = 0, MaxValueFloat = 500, ToolTip = "How much torque is used to rotate the feet to the correct orientation.")]
        public float FootTorque { get; set; }

        [Serialize(50.0f, true), Editable(MinValueFloat = 0, MaxValueFloat = 500, ToolTip = "How much torque is used to rotate the tail to the correct orientation.")]
        public float TailTorque { get; set; }

        [Serialize(null, true), Editable]
        public string FootAngles
        {
            get => ParseFootAngles(FootAnglesInRadians);
            set => SetFootAngles(FootAnglesInRadians, value);
        }

        /// <summary>
        /// Key = limb id, value = angle in radians
        /// </summary>
        public Dictionary<int, float> FootAnglesInRadians { get; set; } = new Dictionary<int, float>();
    }

    interface IFishAnimation
    {
        bool Flip { get; set; }
        string FootAngles { get; set; }
        Dictionary<int, float> FootAnglesInRadians { get; set; }
    }
}
