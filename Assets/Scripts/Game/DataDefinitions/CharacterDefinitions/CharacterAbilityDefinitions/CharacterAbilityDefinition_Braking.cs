using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 020004bc; bodies translated from the 1.10.1 x86-64 binary.
    [CreateAssetMenu(fileName = "CharacterAbilityDefinition_Braking", menuName = "HardlightProject/DefinitionData/AbilityDef/Braking", order = 1)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterAbilityDefinition_Braking : CharacterAbilityDefinition_MovementGround
    {
        [Tooltip("X axis = Time spent braking \n Y axis = Brake power")]
        public AnimationCurve BrakePowerCurve;
        public float BufferedIntentSmoothTimeSeconds = 0.5f;
        [Tooltip("Angle from character forward and intended input direction that will trigger ability.")]
        public float MinimumIntendedTriggerAngle = 160f;
        [Tooltip("Angle from character forward and intended input direction\nAngle less than or equal to this value will exit ability.")]
        public float ExitIntendedAngle = 30f;
        [Tooltip("Speed character must be moving in XZ plane before allowed to trigger ability.")]
        public float MinimumTriggerXZSpeed = 15f;
        [Tooltip("Speed in XZ plane of character that will automatically exit ability.")]
        public float EndAtXZSpeed = 1f;
        [Tooltip("Minimum total input magnitude to enter ability.")]
        public float MinimumInputMagnitude = 0.5f;
        [Tooltip("During this time window, use character forward instead of buffered intent angle for calculations.")]
        public float LandingBufferTimeSeconds = 0.5f;

        public override Type ScriptType => typeof(CharacterAbility_Braking);
        public float MinimumInputMagnitudeSqr { get; private set; }
        public float MinimumTriggerXZSpeedSqr { get; private set; }
        public float EndAtXZSpeedSqr { get; private set; }

        // 06001ae7 (1.10.1 RVA 0x542540): the native body inlines the ground base
        // caching, then squares the three thresholds.
        public override void CalculateCachedValues()
        {
            base.CalculateCachedValues();
            MinimumInputMagnitudeSqr = MinimumInputMagnitude * MinimumInputMagnitude;
            MinimumTriggerXZSpeedSqr = MinimumTriggerXZSpeed * MinimumTriggerXZSpeed;
            EndAtXZSpeedSqr = EndAtXZSpeed * EndAtXZSpeed;
        }

        public CharacterAbilityDefinition_Braking() { }
    }
}
