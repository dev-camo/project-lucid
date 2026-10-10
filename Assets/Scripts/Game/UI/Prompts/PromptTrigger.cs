using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using UnityEngine.Events;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class PromptTrigger : MonoBehaviour
    {
        [SerializeField] private PromptDefinition m_promptDefinition;
        [SerializeField] private UnityEvent m_onShown;
        [SerializeField] private UnityEvent m_onHidden;
        [SerializeField] private List<PromptTriggerChoiceAction> m_promptChoiceActions =
            new List<PromptTriggerChoiceAction>();
        private readonly SystemRef<PromptManager> m_promptManagerRef =
            ProcessManager.GetSystemRef<PromptManager>(null, true);
        private Transform m_target;
        public Action<PromptTrigger> OnHiddenAction;

        // 0x06003733 and 0x06003734; original fields at 0x20 and 0x48.
        public PromptDefinition PromptDefinition => m_promptDefinition;
        public Transform Target => m_target;

        // 0x06003735 and 0x06003736 are original empty lifecycle bodies.
        private void Awake() { }
        private void OnDestroy() { }

        // 0x06003737; Unity equality precedes the transform read.
        private void OnEnable()
        {
            if (m_target == null) m_target = transform;
        }

        // 0x06003738.
        public void SetTarget(Transform target) => m_target = target;

        // 0x06003739; the shown event is reloaded after the manager callback.
        public bool TryShowWithResult()
        {
            if (m_promptManagerRef.TryGet(out PromptManager promptManager) &&
                promptManager.TryShowPrompt(this, OnHidden))
            {
                m_onShown.Invoke();
                return true;
            }
            return false;
        }

        // 0x0600373a; the original discards the result.
        public void TryShow() => TryShowWithResult();

        // 0x0600373b.
        public void TryHide()
        {
            if (m_promptManagerRef.TryGet(out PromptManager promptManager))
                promptManager.TryHidePrompt(this, false);
        }

        // 0x0600373c.
        public void TryAcknowledgeAndDismiss()
        {
            if (m_promptManagerRef.TryGet(out PromptManager promptManager))
                promptManager.TryHidePrompt(this, true);
        }

        // 0x0600373d; callbacks precede the fresh list read. Only the first
        // matching choice fires, and foreach preserves disposal on every exit.
        private void OnHidden(int promptChoice)
        {
            m_onHidden.Invoke();
            OnHiddenAction?.Invoke(this);
            foreach (PromptTriggerChoiceAction promptChoiceAction in m_promptChoiceActions)
            {
                if (promptChoiceAction.TryFire(promptChoice)) break;
            }
        }

        // 0x0600373e; both field initializers run before the MonoBehaviour base.
        public PromptTrigger() { }

        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        private class PromptTriggerChoiceAction
        {
            [SerializeField]
            [Tooltip("If ticked, this triggers when the prompt is dismissed without any user choice (e.g. walking away) and no default choice exists.")]
            private bool m_noChoiceSelected;
            [SerializeField]
            [Tooltip("Prompt choice index to fire on.")]
            [Min(0)]
            [HideIf("m_noChoiceSelected", null)]
            private int m_promptChoiceIndex;
            [SerializeField] private UnityEvent m_onPromptChoice;

            // 0x0600373f; the no-choice option does not suppress index matching.
            public bool TryFire(int promptChoiceIndex)
            {
                if ((promptChoiceIndex == PromptManager.PromptChoiceNone && m_noChoiceSelected) ||
                    promptChoiceIndex == m_promptChoiceIndex)
                {
                    m_onPromptChoice.Invoke();
                    return true;
                }
                return false;
            }

            // 0x06003740; the original only calls System.Object's constructor.
            public PromptTriggerChoiceAction() { }
        }
    }
}
