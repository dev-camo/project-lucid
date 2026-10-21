using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009a1; bodies translated from the 1.10.1 x86-64 binary.
    // A button showing an atlased sprite that is loaded through the addressables manager.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIWidgetButtonImageTexture : UIWidgetButtonImageBase
    {
        [SerializeField] private AssetReferenceAtlasedSprite m_atlasedSpriteToLoad;
        private static readonly SystemRef<AddressableManager> s_addressableManagerRef = ProcessManager.GetSystemRef<AddressableManager>(null, true);

        // 0x060037ba: the base setup runs first. Data of the wrong type only logs.
        public override void Setup(IUIWidgetParameters parameters)
        {
            base.Setup(parameters);
            if (!(parameters is UIWidgetButtonImageTextureParameters data))
            {
                HLOutput.LogError("Could not set up UIWidgetButtonImageTexture with given data.");
                return;
            }
            m_atlasedSpriteToLoad = data.Image;
        }

        // 0x060037bb: loads the sprite once the addressables manager exists, if the key is valid.
        protected override void Start()
        {
            if (m_atlasedSpriteToLoad.RuntimeKeyIsValid())
                s_addressableManagerRef.InvokeOnValid(addressableManager =>
                    m_assetLoadHandle = addressableManager.LoadAssetImmediate<Sprite>(m_atlasedSpriteToLoad, OnImageLoaded));
        }

        // 0x060037bc
        private void OnImageLoaded(Sprite image)
        {
            Image.Value = image;
        }

        // 0x060037bd
        public UIWidgetButtonImageTexture() { }
    }
}
