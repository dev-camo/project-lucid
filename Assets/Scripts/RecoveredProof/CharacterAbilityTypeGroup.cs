using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Option.NullChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "CharacterAbilityTypeGroup", menuName = "HardlightProject/DefinitionData/Groups/CharacterAbilityTypeGroup")]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterAbilityTypeGroup : ScriptableObject
    {
        [UnityEngine.SerializeField]
        private List<HardlightProject.CharacterAbilityTypeGroup.AbilityTypeDefinition> m_abilities = new List<AbilityTypeDefinition>();
        public List<AbilityTypeDefinition> Abilities => m_abilities;
        // Original06001bb9 initializes the owned empty list before base.
        public CharacterAbilityTypeGroup() { }

        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Option.NullChecks, false)]
        [Serializable]
        public class AbilityTypeDefinition
        {
            [UnityEngine.SerializeField]
            [Hardlight.Utils.HashEnum(null)]
            private HardlightProject.ActorAbilityType m_type;
            [UnityEngine.SerializeField]
            private float m_gracePeriod = 0.2f;
            public ActorAbilityType Type => m_type;
            public float GracePeriod => m_gracePeriod;
            // Original06001bbc writes binary32 0.2 before Object base.
            public AbilityTypeDefinition() { }
        }
    }
}
