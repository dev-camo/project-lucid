using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020004af; complete13 own fields/3 methods.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class CharacterAbilityDefinition : AbilityDefinition
    {
        // 0x04001068; original instance offset 0x48.
        [UnityEngine.SerializeField()]
        [UnityEngine.TooltipAttribute("UI to instance to optionally display info for the ability.")]
        private HardlightProject.CharacterAbilityUIType m_uiType;
        // 0x04001069; original instance offset 0x4c.
        [UnityEngine.TooltipAttribute("Only show view info in debug builds.")]
        public bool ViewInfoDebugOnly;
        // 0x0400106a; original instance offset 0x50.
        [UnityEngine.TooltipAttribute("Multiplies the character's current gravity.")]
        public float GravityMultiplier = 1f;
        // 0x0400106b; original instance offset 0x54.
        [UnityEngine.TooltipAttribute("Whether to exit invulnerability immediately on entering.")]
        public bool ExitInvulnerability;
        // 0x0400106c; original instance offset 0x58.
        [UnityEngine.TooltipAttribute("Modify input parameters.")]
        public HardlightProject.CharacterAbilityDefinition.InputModifierLookups InputModifier;
        // 0x0400106d; original instance offset 0x60.
        [UnityEngine.Serialization.FormerlySerializedAsAttribute("CameraOverride")]
        [UnityEngine.TooltipAttribute("Camera type override for the duration of this motion.")]
        public HardlightProject.CameraType CameraTypeOverride;
        // 0x0400106e; original instance offset 0x68.
        [UnityEngine.TooltipAttribute("Settings for affecting the camera proxy that follows the character.")]
        public HardlightProject.CameraProxyTargetSettings CameraProxyTargetSettings;
        // 0x0400106f; original instance offset 0xa8.
        [UnityEngine.TooltipAttribute("Settings for overriding the camera free look heading.")]
        public HardlightProject.CameraRecenterHeadingOverrides FreeLookHeadingOverrides;
        // 0x04001070; original instance offset 0xb0.
        [UnityEngine.TooltipAttribute("Ability continues boost if already activated and brain still has valid input.")]
        public bool ContinuesBoost;
        // 0x04001071; original instance offset 0xb8.
        [UnityEngine.TooltipAttribute("Character boost energy recharge rate.")]
        public UnityEngine.AnimationCurve BoostRechargeRate;
        // 0x04001072; original instance offset 0xc0.
        [UnityEngine.TooltipAttribute("Character chaos energy recharge rate.")]
        public UnityEngine.AnimationCurve ChaosRechargeRate;
        // 0x04001073; original instance offset 0xc8.
        [UnityEngine.TooltipAttribute("If true, blob shadow is disabled while the state for this ability is active.")]
        public bool DisableBlobShadow;
        // 0x04001074; original instance offset 0xc9.
        [UnityEngine.TooltipAttribute("If true, character switching can be activated while this ability is active.")]
        public bool SwitchAvailable;

        // 0x06001aac: direct original field getter.
        public CharacterAbilityUIType UIType { get { return m_uiType; } }

        // 0x06001aad: original base implementation clears out value, then returns false.
        public virtual bool TryGetBoostDefinition(out CharacterStamina.Definition boostDefinition)
        {
            boostDefinition = null;
            return false;
        }

        // 0x06001aae: GravityMultiplier initialization precedes genuine base ctor.
        protected CharacterAbilityDefinition() { }

        // Original nested 0x020004b0; complete own fields and methods.
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Serializable]
        public class InputModifierLookups
        {
            // 0x04001075; original instance offset 0x10.
            [UnityEngine.TooltipAttribute("Directional input angle lookup modifier.")]
            public UnityEngine.AnimationCurve DirectionalAngleLookup;

            // 0x06001aaf: original Object base only; curve remains null.
            public InputModifierLookups() { }
        }

        // Original nested 0x020004b1; complete own fields and methods.
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Serializable]
        public class Motion
        {
            // 0x04001076; original instance offset 0x10.
            [UnityEngine.TooltipAttribute("Clamps velocity to the minimum speed value when falling below it.")]
            public bool ClampToSpeedMin;
            // 0x04001077; original instance offset 0x14.
            [UnityEngine.MinAttribute(0f)]
            [UnityEngine.TooltipAttribute("Minimum speed target traveling in the forward axis.")]
            public float SpeedMin;
            // 0x04001078; original instance offset 0x18.
            [UnityEngine.TooltipAttribute("Maximum speed target traveling in the forward axis.")]
            [UnityEngine.MinAttribute(0f)]
            public float SpeedMax;
            // 0x04001079; original instance offset 0x1c.
            [UnityEngine.MinAttribute(0f)]
            [UnityEngine.TooltipAttribute("Maximum speed cap traveling in the forward axis.")]
            public float SpeedCap;
            // 0x0400107a; original instance offset 0x20.
            [UnityEngine.TooltipAttribute("Acceleration in the forward axis up to the minimum speed.")]
            public float Acceleration;
            // 0x0400107b; original instance offset 0x28.
            [UnityEngine.TooltipAttribute("Deceleration down to the maximum speed.  Time axis is speed above maximum speed.")]
            public UnityEngine.AnimationCurve DecelerationCurve;
            // 0x0400107c; original instance offset 0x30.
            [UnityEngine.TooltipAttribute("Scaling overrides from input.")]
            public HardlightProject.CharacterAbilityDefinition.ThrottleScaling Scaling;
            // 0x0400107d; original instance offset 0x38.
            [UnityEngine.Serialization.FormerlySerializedAsAttribute("CameraOverride")]
            [UnityEngine.TooltipAttribute("Camera type override for the duration of this motion.")]
            public HardlightProject.CameraType CameraTypeOverride;
            // 0x0400107e; original instance offset 0x40.
            [UnityEngine.TooltipAttribute("Camera type override for the duration of this motion if speed within limits.")]
            public Hardlight.IntervalList<float,HardlightProject.CameraType> CameraTypeOverridesBySpeed = new Hardlight.IntervalList<float, CameraType>();

            // 0x06001ab0: original fresh interval-list initialization, then Object ctor.
            public Motion() { }

            // 0x06001ab1: retain allocation-before-copy and shallow reference copies.
            // Camera override and interval list are deliberately not copied by the native body.
            public Motion(Motion motion)
            {
                ClampToSpeedMin = motion.ClampToSpeedMin;
                SpeedMin = motion.SpeedMin;
                SpeedMax = motion.SpeedMax;
                SpeedCap = motion.SpeedCap;
                Acceleration = motion.Acceleration;
                DecelerationCurve = motion.DecelerationCurve;
                Scaling = motion.Scaling;
            }

            // 0x06001ab2: original reference-null/zero-key gate and clamped excess speed.
            public float EvaluateDeceleration(float speed)
            {
                if (DecelerationCurve != null && DecelerationCurve.length != 0)
                    return DecelerationCurve.Evaluate(Mathf.Max(speed - SpeedMax, 0f));
                return 0f;
            }

            // 0x06001ab3: zero cap leaves speed unchanged, including negative zero/NaN.
            public float ClampSpeed(float speed)
            {
                return SpeedCap == 0f ? speed : Mathf.Min(speed, SpeedCap);
            }

            // 0x06001ab4: no null-list fallback; genuine interval lookup precedes field fallback.
            public CameraType GetCameraTypeOverride(float speed)
            {
                CameraType cameraType;
                return CameraTypeOverridesBySpeed.TryGetIntervalValue(speed, out cameraType)
                    ? cameraType : CameraTypeOverride;
            }
        }

        // Original nested 0x020004b2; complete own fields and methods.
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Serializable]
        public class ThrottleScaling
        {
            // 0x0400107f; original instance offset 0x10.
            [UnityEngine.TooltipAttribute("Whether the forward scaling multiplier should override that of the global default.")]
            public bool OverrideForwardScaling;
            // 0x04001080; original instance offset 0x14.
            [UnityEngine.TooltipAttribute("The extent to which positive Y-axis input influences values.")]
            public float ForwardScalingMultiplier = -1f;
            // 0x04001081; original instance offset 0x18.
            [UnityEngine.TooltipAttribute("Whether the backward scaling multiplier should override that of the global default.")]
            public bool OverrideBackwardScaling;
            // 0x04001082; original instance offset 0x1c.
            [UnityEngine.TooltipAttribute("The extent to which negative Y-axis input influences values.")]
            public float BackwardScalingMultiplier = -1f;

            // 0x06001ab5: both multiplier fields initialize to negative one.
            public ThrottleScaling() { }
        }
    }
}
