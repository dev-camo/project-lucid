using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020004e4; complete2 own fields/1 protected ctor.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class CharacterAbilityDefinition_Movement : CharacterAbilityDefinition
    {
        // 0x04001175; original instance offset 0xd0.
        [UnityEngine.TooltipAttribute("Default movement in the forward axis.")]
        public HardlightProject.CharacterAbilityDefinition.Motion ForwardMotion;
        // 0x04001176; original instance offset 0xd8.
        [UnityEngine.TooltipAttribute("Movement override when on different slopes.")]
        public HardlightProject.CharacterAbilityDefinition_Movement.SlopeMotion[] SlopeOverrides;

        // 0x06001b3d: genuine CharacterAbilityDefinition ctor, inlined in both architectures.
        protected CharacterAbilityDefinition_Movement() { }

        // Original nested0x020004e5; complete13 fields/ctor.
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Serializable]
        public class SlopeMotion
        {
            // 0x04001177; original instance offset 0x10.
            [UnityEngine.TooltipAttribute("Minimum angle of range from 0 inclusive to 360 exclusive.")]
            [UnityEngine.RangeAttribute(0f, 360f)]
            public float AngleMin;
            // 0x04001178; original instance offset 0x14.
            [UnityEngine.TooltipAttribute("Maximum angle of range from 0 inclusive to 360 exclusive.")]
            [UnityEngine.RangeAttribute(0f, 360f)]
            public float AngleMax;
            // 0x04001179; original instance offset 0x18.
            [UnityEngine.TooltipAttribute("Defines the forward motion attributes on this slope.")]
            public HardlightProject.CharacterAbilityDefinition.Motion ForwardMotion;
            // 0x0400117a; original instance offset 0x20.
            [UnityEngine.TooltipAttribute("Multiplies the character's current gravity.")]
            public float GravityMultiplier = 1f;
            // 0x0400117b; original instance offset 0x24.
            [UnityEngine.TooltipAttribute("When current speed falls below threshold, the following logic is activated.")]
            [UnityEngine.MinAttribute(0f)]
            public float SpeedThreshold;
            // 0x0400117c; original instance offset 0x28.
            [UnityEngine.TooltipAttribute("Ability determines if character is allowed to turn around.")]
            public bool CanTurnAround = true;
            // 0x0400117d; original instance offset 0x29.
            [UnityEngine.TooltipAttribute("Ability determines character will detach from current surface.")]
            public bool CanDetach;
            // 0x0400117e; original instance offset 0x2a.
            [UnityEngine.TooltipAttribute("Ability determines character is allowed to jump from current surface.")]
            public bool CanJump = true;
            // 0x0400117f; original instance offset 0x2b.
            [UnityEngine.TooltipAttribute("Ability determines character is always orientated to the world up.")]
            public bool OrientateToWorldUp;
            // 0x04001180; original instance offset 0x30.
            [UnityEngine.TooltipAttribute("Time over which grip falls off to zero when turning against gravity.")]
            public UnityEngine.AnimationCurve GripAgainstGravityMultiplierCurve;
            // 0x04001181; original instance offset 0x38.
            [UnityEngine.TooltipAttribute("Time over which grip falls off to zero when turning with gravity.")]
            public UnityEngine.AnimationCurve GripWithGravityMultiplierCurve;
            // 0x04001182; original instance offset 0x40.
            [UnityEngine.TooltipAttribute("Grip multiplier driven by current speed.")]
            public UnityEngine.AnimationCurve GripSpeedMultiplierCurve;
            // 0x04001183; original instance offset 0x48.
            [UnityEngine.TooltipAttribute("Settings for overriding the camera free look heading.")]
            public HardlightProject.CameraRecenterHeadingOverrides FreeLookHeadingOverrides;

            // 0x06001b3e: original three field initializers precede Object ctor.
            public SlopeMotion() { }
        }
    }
}
