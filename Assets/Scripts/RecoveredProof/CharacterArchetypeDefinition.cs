using Hardlight;
using Hardlight.Enums;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "CharacterArchetypeDefinition", menuName = "HardlightProject/DefinitionData/Definitions/CharacterArchetypeDefinition")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterArchetypeDefinition : ScriptableObjectWithGuid
    {
        [SerializeField] private CharacterArchetype m_characterArchetype;
        [Tooltip("Widget to instantiate when a new character archetype is unlocked."), SerializeField]
        private UIWidgetProgression m_progressionUnlockWidget;
        [SerializeField, Tooltip("Whether this archetype should be saved as unlocked by default.")]
        private bool m_unlockedByDefault;
        [HashEnum(typeof(Strings)), SerializeField] private Strings m_lockedMissionStringId;
        [SerializeField] private AssetReferenceAtlasedSprite m_uiImage;
        [SerializeField] private AssetReferenceAtlasedSprite m_alternateImage;
        [SerializeField] private AssetReferenceAtlasedSprite m_silhouetteImage;
        [SerializeField] private AssetReferenceT<Texture> m_texture;

        // Game.Runtime 0x06001bc6..0x06001bc9; ARM64 0x51d450..0x51d468.
        public CharacterArchetype CharacterArchetype => m_characterArchetype;
        public UIWidgetProgression ProgressionUnlockWidget => m_progressionUnlockWidget;
        public bool UnlockedByDefault => m_unlockedByDefault;
        public Strings LockedMissionStringId => m_lockedMissionStringId;

        // Game.Runtime 0x06001bca..0x06001bd1. Original private setters and
        // compiler-generated backing fields retain the four independent wrappers.
        public ManagedAddressableAsset<Sprite> UIImage { get; private set; }
        public ManagedAddressableAsset<Sprite> AlternateImage { get; private set; }
        public ManagedAddressableAsset<Sprite> SilhouetteImage { get; private set; }
        public ManagedAddressableAsset<Texture> Texture { get; private set; }

        // Game.Runtime 0x06001bd2; ARM64 0x51d4b0. Reload each authored reference
        // immediately before constructing and publishing its wrapper, in this order.
        // The original creates fresh wrappers for null references and does not load
        // assets or release any previously published wrapper.
        protected void OnEnable()
        {
            UIImage = new ManagedAddressableAsset<Sprite>(m_uiImage);
            AlternateImage = new ManagedAddressableAsset<Sprite>(m_alternateImage);
            SilhouetteImage = new ManagedAddressableAsset<Sprite>(m_silhouetteImage);
            Texture = new ManagedAddressableAsset<Texture>(m_texture);
        }
        // The implicit public constructor is Game.Runtime 0x06001bd3: original
        // ScriptableObjectWithGuid base call only, with no own field initializers.
    }
}
