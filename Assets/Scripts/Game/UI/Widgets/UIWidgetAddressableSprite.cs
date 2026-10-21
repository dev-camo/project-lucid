using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200099a; bodies translated from the 1.10.1 x86-64 binary.
    // Loads the sprite of an addressable reference into LoadedSprite and releases it on teardown.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIWidgetAddressableSprite : UIWidget
    {
        public readonly Bindable<Sprite> LoadedSprite = new Bindable<Sprite>();
        private readonly SystemRef<AddressableManager> m_addressablesManagerRef = ProcessManager.GetSystemRef<AddressableManager>(null, true);
        private AsyncOperationHandle m_spriteHandle;
        private AssetReferenceT<Sprite> m_spriteReference;

        // 0x06003795
        private void OnDestroy()
        {
            if (m_addressablesManagerRef.IsValid())
                TryUnloadAsset(m_addressablesManagerRef.Get());
        }

        // 0x06003796
        public void SetSpriteReference(AssetReferenceT<Sprite> spriteReference)
        {
            m_spriteReference = spriteReference;
            RefreshBindings();
        }

        // 0x06003797: without a reference nothing happens. Otherwise the previous handle is
        // released and the sprite is loaded synchronously, publishing it through the callback.
        private void RefreshBindings()
        {
            if (m_spriteReference == null)
                return;
            AddressableManager addressableManager = m_addressablesManagerRef.Get();
            TryUnloadAsset(addressableManager);
            m_spriteHandle = addressableManager.LoadAssetImmediate<Sprite>(m_spriteReference, sprite => LoadedSprite.Value = sprite);
        }

        // 0x06003798
        private void TryUnloadAsset(AddressableManager addressableManager)
        {
            if (m_spriteHandle.IsValid())
                addressableManager.ReleaseHandle(m_spriteHandle);
        }

        // 0x06003799
        public UIWidgetAddressableSprite() { }
    }
}
