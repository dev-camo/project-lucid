using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009b0; bodies translated from the 1.10.1 x86-64 binary.
    // A cutscene gallery button: locked entries are inert, unlocked ones load their image.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIWidgetCutscene : UIWidgetButtonImageBase
    {
        public readonly Bindable<bool> IsNew = new Bindable<bool>();

        // 0x060037f6: the original body is empty.
        protected override void Start() { }

        // 0x060037f7: the base setup runs first.
        public override void Setup(IUIWidgetParameters parameters)
        {
            base.Setup(parameters);
            UIWidgetCutsceneParameters data = parameters.GetAs<UIWidgetCutsceneParameters>();
            if (data.Locked)
            {
                Lock();
                return;
            }
            IsNew.Value = data.IsNew;
            SetInteractable(true);
            LoadAddressableImage(data.ImageAssetReference);
        }

        // 0x060037f8: the texture reference is loaded as a sprite through the addressables manager.
        private void LoadAddressableImage(AssetReference imageAssetReference)
        {
            if (imageAssetReference.RuntimeKeyIsValid())
                m_assetLoadHandle = ProcessManager.GetSystem<AddressableManager>(null, true).LoadAsset<Sprite>(imageAssetReference, OnImageLoaded);
        }

        // 0x060037f9
        private void OnImageLoaded(Sprite image)
        {
            Image.Value = image;
        }

        // 0x060037fa
        private void Lock()
        {
            IsNew.Value = false;
            SetInteractable(false);
        }

        // 0x060037fb
        public void SetIsNew(bool isNew)
        {
            IsNew.Value = isNew;
        }

        // 0x060037fc
        public UIWidgetCutscene() { }
    }
}
