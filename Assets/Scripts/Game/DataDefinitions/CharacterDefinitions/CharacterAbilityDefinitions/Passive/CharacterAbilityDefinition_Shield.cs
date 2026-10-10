using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "CharacterAbilityDefinition_Shield", menuName = "HardlightProject/DefinitionData/AbilityDef/Shield", order = 1)]
    public class CharacterAbilityDefinition_Shield : CharacterAbilityDefinition
    {
        [Tooltip("Number of hits the shield will sustain before deactivation.")]
        public int DamageHitPoints = 1;
        [Tooltip("Defines the pfx while shield is active.")]
        public ActorAnimationDefinition ActivationAnimationDefinition;

        public override Type ScriptType => typeof(CharacterAbility_Shield);
        public CharacterAbilityDefinition_Shield() { }
    }
}
