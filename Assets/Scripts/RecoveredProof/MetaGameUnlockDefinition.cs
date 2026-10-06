using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "MetaGameUnlockDefinition", menuName = "HardlightProject/DefinitionData/Definitions/MetaGameUnlockDefinition")]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    public sealed class MetaGameUnlockDefinition : Hardlight.ScriptableObjectWithGuid
    {
        [UnityEngine.SerializeField]
        private HardlightProject.PlayerProgressionTypes m_type;

        [Hardlight.Utils.HashEnum(null)]
        [UnityEngine.SerializeField]
        private Hardlight.Enums.Strings m_title;

        [Hardlight.Utils.HashEnum(null)]
        [UnityEngine.SerializeField]
        private Hardlight.Enums.Strings m_body;

        [Hardlight.Utils.HashEnum(null)]
        [UnityEngine.SerializeField]
        private HardlightProject.HLAudioClipIdentifier m_unlockAudio;

        // Original Game.Runtime 0x06001d45, ARM 0x5249b0.
        public HardlightProject.PlayerProgressionTypes Type => m_type;

        // Original Game.Runtime 0x06001d46, ARM 0x5249b8.
        public Hardlight.Enums.Strings Title => m_title;

        // Original Game.Runtime 0x06001d47, ARM 0x5249c0.
        public Hardlight.Enums.Strings Body => m_body;

        // Original Game.Runtime 0x06001d48, ARM 0x5249c8.
        public HardlightProject.HLAudioClipIdentifier UnlockAudio => m_unlockAudio;

        // Original Game.Runtime 0x06001d49, ARM 0x5249d0.
        // Natural original base-only constructor.
    }
}
