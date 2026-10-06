using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "IntroSequenceDefinition", menuName = "HardlightProject/DefinitionData/Definitions/IntroSequenceDefinition")]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    public class IntroSequenceDefinition : Hardlight.ScriptableObjectWithGuid
    {
        [UnityEngine.SerializeField]
        private HardlightProject.IntroSequenceIdentifier m_identifier;

        [UnityEngine.SerializeField]
        private UnityEngine.AddressableAssets.AssetReferenceT<UnityEngine.GameObject> m_introSequence;

        // Original Game.Runtime 0x06001d2d, ARM 0x524728.
        public HardlightProject.IntroSequenceIdentifier IntroSequenceIdentifier => m_identifier;

        // Original Game.Runtime 0x06001d2e, ARM 0x524730.
        public UnityEngine.AddressableAssets.AssetReferenceT<UnityEngine.GameObject> IntroSequence => m_introSequence;

        // Original Game.Runtime 0x06001d2f, ARM 0x524738.
        // Natural original base-only constructor.
    }
}
