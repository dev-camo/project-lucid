using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HardlightProject
{
    // Original Game.Runtime 0200089e; bodies 0x629020..0x6294B0 (1.10.1 x86-64).
    // Shows the continue button only while the application has at least one controller.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerControllerRequired : UIContainer
    {
        [SerializeField]
        private RectTransform m_continueButton;
        private readonly SystemRef<App> m_appRef = ProcessManager.GetSystemRef<App>(null, true);
        private HLInputModule m_hlInputModule;

        // 060031ce: the current input module is cast to HLInputModule, not tested with "as".
        private void Start()
        {
            m_hlInputModule = (HLInputModule)EventSystem.current.currentInputModule;
            m_hlInputModule.BaseControllerProvider.OnControllerConnectionUpdate += OnControllerConnectionUpdate;
            OnControllerConnectionUpdate();
        }

        // 060031cf
        private void OnDestroy()
        {
            m_hlInputModule.BaseControllerProvider.OnControllerConnectionUpdate -= OnControllerConnectionUpdate;
        }

        // 060031d0
        private void OnControllerConnectionUpdate()
        {
            if (m_appRef.IsNull())
                return;
            bool hasController = m_appRef.Get().ValidateHasAtLeastOneController(m_hlInputModule);
            m_continueButton.gameObject.SetActive(hasController);
        }

        // 060031d1
        public UIContainerControllerRequired() { }
    }
}
