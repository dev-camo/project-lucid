using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "CharacterAbilityUIDefinition", menuName = "HardlightProject/DefinitionData/Definitions/CharacterAbilityUIDefinition")]
    public class CharacterAbilityUIDefinition : UnityEngine.ScriptableObject
    {
        [UnityEngine.SerializeField]
        private HardlightProject.CharacterAbilityUIType m_type;

        [UnityEngine.SerializeField]
        private Hardlight.UIContainerIdentifier m_containerIdentifier;

        // Original Game.Runtime 0x06001bbd, ARM 0x51d288.
        public HardlightProject.CharacterAbilityUIType Type => m_type;

        // Original Game.Runtime 0x06001bbe, ARM 0x51d290.
        public Hardlight.UIContainerIdentifier ContainerIdentifier => m_containerIdentifier;

        // Original Game.Runtime 0x06001bbf, ARM 0x51d298.
        // Natural original constructor; genuine field initializers precede base.
    }
}
