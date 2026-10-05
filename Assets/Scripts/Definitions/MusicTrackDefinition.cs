using System;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "MusicTrackDefinition", menuName = "HardlightProject/DefinitionData/Definitions/MusicTrackDefinition")]
    public class MusicTrackDefinition : ScriptableObjectWithGuid
    {
        [HashEnum(null)]
        [SerializeField]
        private HLAudioClipIdentifier m_identifier;

        [SerializeField]
        [HashEnum(null)]
        private Strings m_name;

        [SerializeField]
        [HashEnum(null)]
        private Strings m_composer;

        [SerializeField]
        private AssetReferenceSprite m_image;

        [SerializeField]
        private AssetReferenceTexture m_imageAsTexture;

        [SerializeField]
        private bool m_alwaysUnlocked;

        [SerializeField]
        private UIWidgetProgression m_progressionUnlockWidget;

        [SerializeField]
        private bool m_hiddenInJukebox;

        [NonSerialized]
        private ManagedAddressableAsset<Sprite> m_imageAsset;

        [NonSerialized]
        private ManagedAddressableAsset<Texture> m_imageAssetTexture;

        // Game.Runtime 0x06001e62..0x06001e69: original direct field getters.
        public HLAudioClipIdentifier ClipIdentifier => m_identifier;
        public Strings Name => m_name;
        public Strings Composer => m_composer;
        public bool AlwaysUnlocked => m_alwaysUnlocked;
        public UIWidgetProgression ProgressionUnlockWidget => m_progressionUnlockWidget;
        public ManagedAddressableAsset<Sprite> ImageAsset => m_imageAsset;
        public ManagedAddressableAsset<Texture> ImageAssetTexture => m_imageAssetTexture;
        public bool HiddenInJukebox => m_hiddenInJukebox;

        // Game.Runtime 0x06001e6a: fresh wrappers in this order, retaining the exact references.
        // There is no null gate, asset load, old-wrapper release or reuse branch.
        protected void OnEnable()
        {
            m_imageAsset = new ManagedAddressableAsset<Sprite>(m_image);
            m_imageAssetTexture = new ManagedAddressableAsset<Texture>(m_imageAsTexture);
        }

        // Game.Runtime 0x06001e6b: only the genuine GUID base constructor; own fields stay zero/null.
        public MusicTrackDefinition() { }
    }
}
