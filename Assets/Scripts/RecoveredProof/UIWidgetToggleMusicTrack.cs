using Hardlight;
using Hardlight.Enums;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime020009d7, nine own APIs including the genuine Setup lambda.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIWidgetToggleMusicTrack : UIWidget
    {
        [SerializeField] private UIToggle m_toggle;
        [SerializeField] private UIToggleGroupElement m_toggleGroupElement;
        public readonly Bindable<string> Number = new Bindable<string>();
        public readonly Bindable<Sprite> Image = new Bindable<Sprite>();
        public readonly Bindable<Strings> Name = new Bindable<Strings>();
        public readonly Bindable<Strings> Composer = new Bindable<Strings>();
        public readonly Bindable<bool> IsNew = new Bindable<bool>();
        private UIWidgetToggleMusicTrackParameters m_parameters;

        // Original060038a5/38a6: mandatory real toggle events, without object/null guards.
        private void Start() { m_toggle.OnValueChanged.AddListener(OnValueChanged); }
        private void OnDestroy() { m_toggle.OnValueChanged.RemoveListener(OnValueChanged); }

        // Original060038a7; fields are reread after each virtual bindable/callback operation.
        public override void Setup(IUIWidgetParameters parameters)
        {
            base.Setup(parameters);
            m_parameters = parameters.GetAs<UIWidgetToggleMusicTrackParameters>();
            Number.Value = m_parameters.Number.ToString();
            Name.Value = m_parameters.Name;
            Composer.Value = m_parameters.Composer;
            IsNew.Value = m_parameters.IsNew && !m_parameters.Selected;
            // Original060038ad directly updates Image through its virtual setter.
            m_parameters.Image.LoadAsync(sprite => Image.Value = sprite);
            m_toggle.IsOn = m_parameters.Selected;
            m_toggleGroupElement.Group = m_parameters.ToggleGroup;
            EnsureCorrectVisualState(m_toggle.IsOn);
        }

        // Original060038a8: transition first, required caller second, fresh IsNew setter last.
        private void OnValueChanged(bool isOn)
        {
            EnsureCorrectVisualState(isOn);
            m_parameters.OnValueChanged(isOn);
            IsNew.Value = false;
        }

        // Original060038a9: locked chooses Disabled4, otherwise Normal0; Selected3 follows independently.
        private void EnsureCorrectVisualState(bool isOn)
        {
            m_toggle.Transition.QueueTransitionToState(m_parameters.Locked ? UITransitionState.Disabled : UITransitionState.Normal);
            if (isOn) m_toggle.Transition.QueueTransitionToState(UITransitionState.Selected);
        }

        // Original060038aa.
        public void SetSelected(bool selected) { m_toggle.IsOn = selected; }

        // Original060038ab: read the callback before evaluating the transform and exact cast.
        public void Action_OnHighlighted() { m_parameters.OnHighlighted((RectTransform)transform); }

        // Original060038ac: the five bindables allocate in field order before the UIWidget constructor.
        public UIWidgetToggleMusicTrack() { }
    }
}
