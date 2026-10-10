using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "DreamPowerStoreDefinition", menuName = "HardlightProject/DefinitionData/Definitions/DreamPowerStoreDefinition")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class DreamPowerStoreDefinition : ScriptableObjectWithGuid
    {
        [SerializeField] private AssetReferenceTexture m_image;
        [SerializeField] private UIWidgetProgression m_progressionUnlockWidget;
        [SerializeField] private int m_startingBlueCoinCount;
        [System.NonSerialized] private ManagedAddressableAsset<Texture> m_imageAsset;

        // Original 06001c6b/6c/6d: direct field accessors.
        public UIWidgetProgression ProgressionUnlockWidget => m_progressionUnlockWidget;
        public ManagedAddressableAsset<Texture> ImageAsset => m_imageAsset;
        public int StartingBlueCoinCount => m_startingBlueCoinCount;

        // Original 06001c6e captures the reference first, then publishes the
        // new wrapper. It does not call the inherited OnEnable lifecycle.
        protected void OnEnable()
        {
            AssetReferenceTexture reference = m_image;
            m_imageAsset = new ManagedAddressableAsset<Texture>(reference);
        }

        // Original 06001c6f calls the real GUID base without field defaults.
        public DreamPowerStoreDefinition() { }
    }
}
