using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIRuntimeConfiguration : MonoBehaviour, ISystem
    {
        [SerializeField] private InputSupplier m_scrollUpInput;
        [SerializeField] private InputSupplier m_scrollDownInput;
        [HashEnum(typeof(HLAudioTypes))]
        [SerializeField] private HLAudioTypes m_navigateTowardsSound;
        [SerializeField] private BaseInputModule m_inputModule;
        [SerializeField] private InputBridge m_inputBridge;
        [Tooltip("Should the UI system highlight the active selection?")]
        [SerializeField] private bool m_shouldHighlightSelection;
        [Tooltip("Should the UI system cope with autorotation?")]
        [SerializeField] private bool m_shouldAutoRotateScreen;
        [SerializeField]
        [Tooltip("Should the UI system automatically navigate when no explicit button navigation is specified?")]
        private bool m_shouldAutoNavigateUISelection;

        // Original getters 060000bc..060000c3 retain property declaration order.
        public InputSupplier ScrollUpInput => m_scrollUpInput;
        public InputSupplier ScrollDownInput => m_scrollDownInput;
        public HLAudioTypes NavigateTowardsSound => m_navigateTowardsSound;
        public InputBridge InputBridge => m_inputBridge;
        public BaseInputModule InputModule => m_inputModule;
        public bool ShouldHighlightSelection => m_shouldHighlightSelection;
        public bool ShouldAutoRotateScreen => m_shouldAutoRotateScreen;
        public bool ShouldAutoNavigateUISelection => m_shouldAutoNavigateUISelection;

        // Original 060000c4 registers first, then rereads the bridge between the two virtual calls.
        private void Awake()
        {
            ProcessManager.RegisterSystem(this, null, false, false);
            m_inputBridge.SetInputModule(m_inputModule);
            m_inputBridge.RegisterForInputUpdateEvents();
        }

        // Original 060000c5 has no finally or bridge-null guard.
        private void OnDestroy()
        {
            ProcessManager.UnregisterSystem(this);
            m_inputBridge.UnregisterForInputUpdateEvents();
        }

        // Original 060000c6 writes no authored defaults before or after the base constructor.
        public UIRuntimeConfiguration() { }
    }
}
