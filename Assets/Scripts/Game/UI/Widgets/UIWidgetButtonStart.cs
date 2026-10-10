using Hardlight;
using Hardlight.UI.Binding;
using UnityEngine;
using UnityEngine.UI;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIWidgetButtonStart : UIInteractable
    {
        [SerializeField] private Button m_button;
        [SerializeField] private UITransitionAnimation m_transition;
        [SerializeField] private AnimationClip m_unlockAnimationClip;
        [SerializeField] private PromptTrigger m_isNewPromptTrigger;
        public readonly Bindable<bool> IsNew = new Bindable<bool>();

        // Original 060037c1/2: disabled transition, button mutation, inherited mutation.
        public override bool Interactable
        {
            get => base.Interactable;
            set
            {
                if (m_transition != null && !value)
                    m_transition.QueueTransitionToState(UITransitionState.Disabled);
                m_button.interactable = value;
                base.Interactable = value;
            }
        }

        // Original 060037c3: replace the click listener before inspecting the parameters.
        public void Setup(IUIWidgetParameters parameters)
        {
            UIWidgetButtonStartParameters startParameters = parameters.GetAs<UIWidgetButtonStartParameters>();
            m_button.onClick.RemoveListener(OnButtonClick);
            m_button.onClick.AddListener(OnButtonClick);
            if (startParameters.Locked)
            {
                Interactable = false;
                IsNew.Value = false;
                return;
            }
            m_transition.QueueTransitionToState(UITransitionState.Normal);
            Interactable = true;
            IsNew.Value = startParameters.IsNew;
            if (IsNew.Value && m_isNewPromptTrigger != null)
                m_isNewPromptTrigger.TryShow();
        }

        // Original 060037c4: inherited teardown precedes the fresh button event read.
        protected override void OnDestroy()
        {
            base.OnDestroy();
            m_button.onClick.RemoveListener(OnButtonClick);
        }

        // Original 060037c5: queue the genuine completion callback before later mutations.
        public void Unlock()
        {
            m_transition.QueueCustomAnimation(m_unlockAnimationClip, OnUnlockComplete);
            Interactable = true;
            IsNew.Value = true;
        }

        // Original 060037c6.
        private void OnUnlockComplete()
        {
            m_transition.QueueTransitionToState(UITransitionState.Normal);
            if (m_isNewPromptTrigger != null) m_isNewPromptTrigger.TryShow();
        }

        // Original 060037c7.
        private void OnButtonClick()
        {
            if (m_isNewPromptTrigger != null) m_isNewPromptTrigger.TryAcknowledgeAndDismiss();
        }

        // Original 060037c8: the bindable field initializer precedes the genuine base constructor.
        public UIWidgetButtonStart() { }
    }
}
