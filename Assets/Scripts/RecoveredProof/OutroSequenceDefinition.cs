using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "OutroSequenceDefinition", menuName = "HardlightProject/DefinitionData/Definitions/OutroSequenceDefinition")]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    public class OutroSequenceDefinition : Hardlight.ScriptableObjectWithGuid
    {
        [UnityEngine.SerializeField]
        private HardlightProject.OutroSequenceIdentifier m_identifier;

        [UnityEngine.SerializeField]
        private UnityEngine.AddressableAssets.AssetReferenceT<UnityEngine.GameObject> m_outroSequence;

        // Original Game.Runtime 0x06001e7e, ARM 0x52b9f8.
        public HardlightProject.OutroSequenceIdentifier Identifier => m_identifier;

        // Original Game.Runtime 0x06001e7f, ARM 0x52ba00.
        public UnityEngine.AddressableAssets.AssetReferenceT<UnityEngine.GameObject> OutroSequence => m_outroSequence;

        // Original Game.Runtime 0x06001e80, ARM 0x52ba08.
        // Natural original base-only constructor.
    }
}
