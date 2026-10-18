using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020006a6; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class InputEventSelectedOnly : InputEvent
    {
        [Tooltip("Optional UI component that will block input if not interactable.")]
        [SerializeField]
        private UIInteractable m_interactable;
        private readonly SystemRef<UINavigationManager> m_navigationManagerRef = ProcessManager.GetSystemRef<UINavigationManager>();

        // 060023ad (1.10.1 RVA 0x589C60): forwards only while this object holds the navigation selection.
        protected override void OnInput(float amount)
        {
            if (m_interactable != null && !m_interactable.Interactable) return;
            if (m_navigationManagerRef.IsNull()) return;
            UIWidgetNavigation selected = m_navigationManagerRef.Get().GetCurrentlySelected();
            if (selected == null || selected.gameObject != gameObject) return;
            base.OnInput(amount);
        }

        // 060023ae (1.10.1 RVA 0x589DA0)
        public InputEventSelectedOnly()
        {
        }
    }
}
