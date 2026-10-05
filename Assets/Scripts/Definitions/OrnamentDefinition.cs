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
    [CreateAssetMenu(fileName = "OrnamentDefinition", menuName = "HardlightProject/DefinitionData/Definitions/OrnamentDefinition")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class OrnamentDefinition : ScriptableObjectWithGuid
    {
        [SerializeField]
        private OrnamentIdentifier m_identifier;

        [SerializeField]
        private AssetReferenceT<GameObject> m_ornament;

        [SerializeField]
        private OrnamentCategory m_category;

        [ShowIf("m_category", OrnamentCategory.Achievement)]
        [SerializeField]
        private AchievementIdentifier m_unlockedByAchievement;

        [SerializeField]
        [HashEnum(typeof(Strings))]
        private Strings m_localisedName;

        [SerializeField]
        [HashEnum(typeof(Strings))]
        private Strings m_localisedUnlockedLore;

        [SerializeField]
        [HashEnum(typeof(Strings))]
        private Strings m_localisedLockedTip;

        [SerializeField]
        [ShowIf("m_category", OrnamentCategory.Story)]
        private int m_actNumber;

        [SerializeField]
        private AssetReferenceT<Texture> m_thumbnailAsset;

        [SerializeField]
        private AssetReferenceTexture m_thumbnailUncropped;

        [Tooltip("Widget to instantiate as part of the unlocked screen.")]
        [SerializeField]
        private UIWidgetProgression m_progressionUnlockWidget;

        [NonSerialized]
        private ManagedAddressableAsset<Texture> m_thumbnailUncroppedAsset;

        public ManagedAddressableAsset<Texture> ThumbnailAsset;

        // Game.Runtime 0x06001e6f..0x06001e78: original direct field getters.
        public OrnamentIdentifier OrnamentIdentifier => m_identifier;
        public AssetReferenceT<GameObject> Ornament => m_ornament;
        public OrnamentCategory OrnamentCategory => m_category;
        public Strings LocalisedName => m_localisedName;
        public Strings LocalisedUnlockedLore => m_localisedUnlockedLore;
        public Strings LocalisedLockedTip => m_localisedLockedTip;
        public AchievementIdentifier UnlockedByAchievement => m_unlockedByAchievement;
        public UIWidgetProgression ProgressionUnlockWidget => m_progressionUnlockWidget;
        public int ActNumber => m_actNumber;
        public ManagedAddressableAsset<Texture> ThumbnailUncroppedAsset => m_thumbnailUncroppedAsset;

        // Game.Runtime 0x06001e79: uncropped wrapper first, public thumbnail wrapper second.
        // Preserve replacement on each call without adding a release or a null check.
        protected void OnEnable()
        {
            m_thumbnailUncroppedAsset = new ManagedAddressableAsset<Texture>(m_thumbnailUncropped);
            ThumbnailAsset = new ManagedAddressableAsset<Texture>(m_thumbnailAsset);
        }

        // Game.Runtime 0x06001e7a: only the genuine GUID base constructor; own fields stay zero/null.
        public OrnamentDefinition() { }
    }
}
