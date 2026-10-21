using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009d9; bodies translated from the 1.10.1 x86-64 binary.
    // A page tab of a paged list: it can be locked, shows a new marker until selected and raises an event when selected.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIWidgetTogglePagination : UIWidgetToggle
    {
        [SerializeField] private UIToggleGroupElement m_toggleGroupElement;
        [SerializeField] private UnityEvent m_onSelected;
        [SerializeField] private AnimationClip m_unlockAnimationClip;
        public readonly Bindable<bool> IsNew = new Bindable<bool>();
        public readonly Bindable<bool> IsVisible = new Bindable<bool>();
        private UIWidgetTogglePaginationParameters m_parameters;

        // 0x060038af (0x6A4E90)
        public UIToggleGroupElement ToggleGroupElement => m_toggleGroupElement;

        // 0x060038b0 (0x6A4EA0): the new marker follows the toggle's selection; the listener is removed before it is added so
        // that a repeated setup leaves one subscription.
        public override void Setup(IUIWidgetParameters parameters)
        {
            base.Setup(parameters);
            m_parameters = parameters.GetAs<UIWidgetTogglePaginationParameters>();
            if (m_parameters != null)
            {
                IsVisible.Value = m_parameters.IsVisible;
                IsNew.Value = !m_toggle.IsOn && m_parameters.IsNew;
                m_toggle.OnValueChanged.RemoveListener(RefreshIsNew);
                m_toggle.OnValueChanged.AddListener(RefreshIsNew);
            }
        }

        // 0x060038b1 (0x6A5070)
        private void OnEnable()
        {
            StartCoroutine(this.WaitForUI(Refresh));
        }

        // 0x060038b2 (0x6A50F0): a locked page is disabled; an unlocked page whose unlock has not been seen plays the unlock animation.
        private void Refresh()
        {
            if (m_parameters == null)
                return;
            if (m_parameters.Locked)
                Lock();
            else if (!m_parameters.UnlockSeen)
                Unlock();
        }

        // 0x060038b3 (0x6A5150)
        private void Lock()
        {
            SetInteractable(false);
            m_toggleGroupElement.enabled = false;
        }

        // 0x060038b4 (0x6A5180)
        private void Unlock()
        {
            m_toggle.Transition.QueueCustomAnimation(m_unlockAnimationClip, null);
        }

        // 0x060038b5 (0x6A5030)
        private void RefreshIsNew(bool isOn)
        {
            IsNew.Value = m_parameters.IsNew && !isOn;
        }

        // 0x060038b6 (0x6A51A0)
        public override void Action_OnValueChanged(bool value)
        {
            base.Action_OnValueChanged(value);
            if (value && m_onSelected != null)
                m_onSelected.Invoke();
        }

        // 0x060038b7 (0x6A51E0)
        public UIWidgetTogglePagination() { }
    }
}
