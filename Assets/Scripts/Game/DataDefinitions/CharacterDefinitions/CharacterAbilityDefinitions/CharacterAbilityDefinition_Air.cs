using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterAbilityDefinition_Air : CharacterAbilityDefinition
    {
        // Original 0x04001083; own offset 0xca.
        [Tooltip("Signals that this will be the first evaluated ability in the fsm flow.  Only one primary ability of the type is allowed.")]
        public bool IsPrimary;
        // Original 0x04001084; own offset 0xcc.
        [Tooltip("Ability is only active when input type is met:\nNone - No input required,\nSingle - A single input will enable for the entire duration,\nContinuous - Enabled while input active but can be re-enabled when active again.")]
        public AbilityInputType Input;
        // Original 0x04001085; own offset 0xd0.
        [Min(1)]
        [Tooltip("The total number of times activation is allowed before a reset is required.")]
        public int ActivationCount = 1;
        // Original 0x04001086; own offset 0xd4.
        [Tooltip("Whether this air ability can be recharged.")]
        public bool CanRecharge = true;
        // Original 0x04001087; own offset 0xd8.
        [Tooltip("Overrides actor animation parameters for each activation.")]
        public List<ActorAnimationDefinition> AnimationActivationOverrides = new List<ActorAnimationDefinition>();
        // Original 0x04001088; own offset 0xe0.
        [Tooltip("If checked then air component speed is added to the current velocity direction.")]
        public bool IsAdditive;
        // Original 0x04001089; own offset 0xe1.
        [Tooltip("If checked then air component speed in the gravity direction is maintained.")]
        public bool MaintainGravitySpeed;
        // Original 0x0400108a; own offset 0xe2.
        [Tooltip("If checked, orientation will snap to input direction on entering ability.")]
        public bool OrientateToInputOnEnter;
        // Original 0x0400108b; own offset 0xe3.
        [Tooltip("If checked then orientation is set from velocity direction.")]
        public bool MaintainOrientationFromVelocity;
        // Original 0x0400108c; own offset 0xe4.
        [Tooltip("If checked then character up is orientated to current gravity.")]
        public bool OrientateToGravity = true;
        // Original 0x0400108d; own offset 0xe5.
        [Tooltip("Ability remains active until interrupted by another ability or leaving the air.")]
        public bool RemainActiveUntilInterrupted;
        // Original 0x0400108e; own offset 0xe6.
        [Tooltip("Ability remains active until interrupted by a projected ground hit.")]
        public bool InterruptFromProjectedGroundHit;
        // Original 0x0400108f; own offset 0xe8.
        [Tooltip("Air component speed and direction.")]
        public List<CharacterAbilityDefinition_Air.AirComponent> Components;
        // Original 0x04001090; own offset 0xf0.
        [Tooltip("Acceleration from current to target velocity. 0 snaps to target.")]
        public Vector3 Acceleration;
        // Original 0x04001091; own offset 0x100.
        [Tooltip("Turn traits for this air ability.")]
        public CharacterTraits.TurnTraits Turn;
        // Original 0x04001092; own offset 0x108.
        [Tooltip("Use motion scaling overrides from input.")]
        public bool UseMotionScaling;
        // Original 0x04001093; own offset 0x110.
        [Tooltip("Motion scaling overrides from input.")]
        public CharacterAbilityDefinition.Motion MotionScaling;
        // Original 0x04001094; own offset 0x118.
        [Tooltip("Multiplied by final local velocity.")]
        public Vector3 ResultingLocalVelocityMultiplier = Vector3.one;
        // Original 0x04001095; own offset 0x128.
        [Tooltip("Blend parameters to restore after the air ability ends.")]
        public VelocityRestoreParameters VelocityStorageParameters;
        // Original 0x04001096; own offset 0x130.
        [Tooltip("If any direction modifier is applied to the character, deactivate this ability.")]
        public bool DeactivateOnDirectionModifier;
        // Original 0x04001097; own offset 0x134.
        [Tooltip("Contact with non-track slope min angle deactivates this ability.")]
        public float DeactivateOnNonTrackSlopeAngleMin = 5f;
        // Original 0x04001098; own offset 0x138.
        [Tooltip("Contact with non-track slope max angle deactivates this ability.")]
        public float DeactivateOnNonTrackSlopeAngleMax = 85f;
        // Original 0x04001099; own offset 0x13c.
        [Tooltip("If change in gravity direction exceeds angle, deactivate this ability.")]
        public float DeactivateOnGravityAngleChange = 45f;
        // Original 0x0400109a; own offset 0x140.
        [Tooltip("If collision with collider reduces velocity below the threshold, deactivate this ability.")]
        public float DeactivateOnCollisionVelocityThreshold;
        // Original 0x0400109b; own offset 0x144.
        [Tooltip("If deactivation logic is triggered, delay interruption to air ability for this period of time.")]
        public float DeactivateTime;
        // Original 0x0400109c; own offset 0x148.
        [Tooltip("If deactivation logic is triggered, zero velocity if no input is applied.")]
        public bool DeactivateToZeroVelocityIfNoInput;
        // Original 0x0400109d; own offset 0x14c.
        [Tooltip("Distance to track that forces ability to continue until landed.")]
        public float TrackDistanceForceContinue;
        // Original 0x0400109e; own offset 0x150.
        [Tooltip("Particle effect trigger activated when ability is recharged.")]
        public ActorParticleTriggerType RechargePFX;
        // Original 0x0400109f; own offset 0x154.
        [Tooltip("Determines if, when switching from another character, can be activated when in air.")]
        public bool PostSwitchCanActivate;
        // Original 0x040010a0; own offset 0x158.
        [Tooltip("When switching from another character, stamina to be set whilst in air.")]
        public float PostSwitchStamina;
        // Original 0x040010a1; own offset 0x15c.
        [Tooltip("If true, on entering the ability and before aligning to gravity, if the character's up direction is perpendicular to gravity, move their position slightly character-up-wards.")]
        [Header("Nudge on Enter")]
        public bool NudgeCharacterUpOnEnter;
        // Original 0x040010a2; own offset 0x160.
        [Tooltip("Maximum absolute dot product between character's up direction and gravity normal, on entering the ability, for nudge to be applied.")]
        [ShowIf("NudgeCharacterUpOnEnter", null)]
        [Range(0, 1)]
        public float NudgeCharacterUpOnEnterMaxDotProduct;
        // Original 0x040010a3; own offset 0x164.
        [Tooltip("Distance of the nudge to apply.")]
        [ShowIf("NudgeCharacterUpOnEnter", null)]
        public float NudgeCharacterUpOnEnterDistance;

        public override Type ScriptType => typeof(CharacterAbility_Air);
        public float DeactivateOnNonTrackSlopeCosineAngleMin { get; private set; }
        public float DeactivateOnNonTrackSlopeCosineAngleMax { get; private set; }
        public float DeactivateOnGravityCosineAngleChange { get; private set; }
        public override void CalculateCachedValues()
        {
            base.CalculateCachedValues();
            DeactivateOnNonTrackSlopeCosineAngleMin = Mathf.Cos(DeactivateOnNonTrackSlopeAngleMin * Mathf.Deg2Rad);
            DeactivateOnNonTrackSlopeCosineAngleMax = Mathf.Cos(DeactivateOnNonTrackSlopeAngleMax * Mathf.Deg2Rad);
            DeactivateOnGravityCosineAngleChange = Mathf.Cos(DeactivateOnGravityAngleChange * Mathf.Deg2Rad);
        }
        public CharacterAbilityDefinition_Air() { }

        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Serializable]
        public class AirComponent
        {
            [Tooltip("Time to start blending to the component velocity.")]
            public float StartTime;
            [Tooltip("Component velocity blend over time.")]
            public AnimationCurve BlendCurve;
            [Tooltip("Direction to apply the speed.")]
            public Vector3 Direction;
            [Tooltip("Speed values over time.")]
            public AnimationCurve SpeedCurve;
            public AirComponent() { }
        }
    }
}
