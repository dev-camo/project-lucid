using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace HardlightProject
{
    // Original Game.Runtime0200099e: three native bodies and one true abstract Start API.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class UIWidgetButtonImageBase : UIWidgetButton
    {
        public readonly Bindable<Sprite> Image = new Bindable<Sprite>();
        [SerializeField] private Sprite m_loadingImage;
        protected AsyncOperationHandle<Sprite> m_assetLoadHandle;

        // Original060037ae: real parent Setup precedes the genuine parameter-type test.
        public override void Setup(IUIWidgetParameters parameters)
        {
            base.Setup(parameters);
            if (parameters is UIWidgetButtonParameters) Image.Value = m_loadingImage;
            else HLOutput.LogError("Could not set up UIWidgetButtonImageBase with given data.");
        }

        // Original060037af is abstract in the shipped metadata; no body is fabricated.
        protected abstract void Start();

        // Original060037b0: valid-handle test then real manager release, retaining the field.
        private void OnDisable()
        {
            if (m_assetLoadHandle.IsValid()) AddressableManager.ReleaseHandleInManager(m_assetLoadHandle);
        }

        // Original060037b1: field allocation precedes the original UIWidgetButton constructor.
        protected UIWidgetButtonImageBase() { }
    }
}
