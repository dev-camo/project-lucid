using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLModernUI.Runtime 0200001c, complete sixteen own APIs; genuine base and transition providers required.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    [Il2CppSetOption(Option.NullChecks, false)]
    [AddComponentMenu("Hardlight/HLModernUI/UIToggle")]
    public class UIToggle : UIInteractable, IPointerClickHandler, ISubmitHandler, ICanvasElement
    {
        [SerializeField] [Tooltip("Is the toggle currently on or off?")] private bool m_isOn;
        [SerializeField] private UIToggleGroupElement m_groupElement;
        [SerializeField] private UITransitionAnimation m_transition;
        public UIToggleEvent OnValueChanged = new UIToggleEvent();

        // Original 0600007a/7b/7c/7d.
        public UIToggleGroupElement GroupElement => m_groupElement;
        public UITransitionAnimation Transition => m_transition;
        public bool IsOn { get => m_isOn; set => Set(value); }

        // Original 0600007e/7f: the disabled transition precedes the inherited mutation and notification.
        public override bool Interactable
        {
            get => base.Interactable;
            set
            {
                if (m_transition != null && !value) m_transition.QueueTransitionToState(UITransitionState.Disabled);
                base.Interactable = value;
            }
        }

        // Original 06000080.
        public void SetIsOnWithoutNotify(bool value) { Set(value, false); }
        // Original 06000081; event-data access happens before active/interactable checks.
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            InternalToggle();
        }
        // Original 06000082; its argument is unused.
        public void OnSubmit(BaseEventData eventData) { InternalToggle(); }
        // Original 06000083/84/85, three genuine empty interface implementations.
        public void Rebuild(CanvasUpdate executing) { }
        public void LayoutComplete() { }
        public void GraphicUpdateComplete() { }

        // Original 06000086: group callbacks precede the transition and profiler/event callbacks; retain fresh fields.
        private void Set(bool value, bool notify = true)
        {
            if (m_isOn == value) return;
            m_isOn = value;
            UIToggleGroup group = m_groupElement != null ? m_groupElement.Group : null;
            if (group != null && group.isActiveAndEnabled && IsActive())
            {
                if (m_isOn || (!group.AnyTogglesOn() && !group.AllowSwitchOff))
                {
                    m_isOn = true;
                    group.NotifyToggleOn(m_groupElement, notify);
                }
            }
            if (m_transition != null)
                m_transition.QueueTransitionToState(Interactable ? (m_isOn ? UITransitionState.Selected : UITransitionState.Normal) : UITransitionState.Disabled);
            if (notify)
            {
                UISystemProfilerApi.AddMarker("UIToggle.value", this);
                OnValueChanged.Invoke(m_isOn);
            }
        }

        // Original 06000087: the original inherited IsActive check precedes the nonvirtual IsInteractable check.
        private void InternalToggle()
        {
            if (!IsActive() || !IsInteractable()) return;
            IsOn = !IsOn;
        }

        // Original 06000088; the genuine event initializer precedes the complete base constructor.
        public UIToggle() { }
        // Original 06000089.
        Transform ICanvasElement.transform => transform;
    }
}
