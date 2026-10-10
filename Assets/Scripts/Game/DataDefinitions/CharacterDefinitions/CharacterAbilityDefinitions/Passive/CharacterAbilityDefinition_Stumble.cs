using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "CharacterAbilityDefinition_Stumble", menuName = "HardlightProject/DefinitionData/AbilityDef/Stumble", order = 1)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterAbilityDefinition_Stumble : CharacterAbilityDefinition
    {
        [Tooltip("Animation to play on stumble activation.")]
        [SerializeField]
        private ActorAnimationDefinition m_activationAnimationDefinition;
        [Tooltip("Duration to stumble for.")]
        [SerializeField]
        private float m_durationSeconds;

        public ActorAnimationDefinition ActivationAnimationDefinition => m_activationAnimationDefinition;
        public float DurationSeconds => m_durationSeconds;
        public override Type ScriptType => typeof(CharacterAbility_Stumble);
        public CharacterAbilityDefinition_Stumble() { }
    }
}
