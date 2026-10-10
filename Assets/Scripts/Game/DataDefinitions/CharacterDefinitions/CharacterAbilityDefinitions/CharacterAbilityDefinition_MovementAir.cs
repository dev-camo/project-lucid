using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "CharacterAbilityDefinition_MovementAir", menuName = "HardlightProject/DefinitionData/AbilityDef/MovementAir", order = 1)]
    public class CharacterAbilityDefinition_MovementAir : CharacterAbilityDefinition_MovementFree
    {
        [Min(0f)]
        [Header("Air settings")]
        [Tooltip("Time after entering ability that landing projection logic begins.")]
        public float LandingProjectionBufferTime = 1f;
        [Tooltip("Landing projection align to world up rate in degrees per second.")]
        [Min(0f)]
        public float LandingProjectionAlignToWorldUpRate = 100f;
        [Tooltip("Maximum allowed world velocity downward.")]
        [Min(0f)]
        public float TerminalVelocity = 100f;
        [Min(0f)]
        [Tooltip("Minimum duration to activate animation triggers for during direction override.")]
        public float MinimumDirectionOverrideAnimationDurationSeconds = 0.1f;
        [Tooltip("Time after entering ability that rolling is deactivated.")]
        [Min(0f)]
        public float RollingDeactivationTime = 0.5f;

        public override Type ScriptType => typeof(CharacterAbility_MovementAir);
        public CharacterAbilityDefinition_MovementAir() { }
    }
}
