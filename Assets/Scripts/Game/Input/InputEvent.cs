using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using ProjectLucid.Offline;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 020006a5; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class InputEvent : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The input required to trigger the events.")]
        private GameInput m_input = GameInput.None;
        [SerializeField]
        [Tooltip("If not triggered immediately, will wait until next frame.")]
        private bool m_triggerImmediately = true;
        [Tooltip("The events to be triggered on input.")]
        [SerializeField]
        private UnityEvent m_event;
        [SerializeField]
        [Tooltip("Set if this should only trigger if only this list of containers are open.")]
        private List<UIContainerIdentifier> m_exclusiveContainers;
        [SerializeField] private List<UIContainerIdentifier> m_ignoreIfContainerIsOpen = new List<UIContainerIdentifier>();
        [SerializeField] private HLAudioSourceIdentifier m_audioSourceIdentifier;
        [SerializeField]
        [HashEnum(null)]
        private HLAudioClipIdentifier m_audioClipIdentifier;
        private readonly SystemRef<AudioManager> m_audioManagerRef = ProcessManager.GetSystemRef<AudioManager>();
        private bool m_isSuspended;

        // 060023a5 (1.10.1 RVA 0x589690): the callback is the virtual OnInput.
        private void OnEnable()
        {
            // Offline hook H-078: D14 hides the independently authored cross-promo controller prompt
            // before original 0x060023a5 @0x589690 subscribes its virtual input callback.
            if (OfflineAdapters.Active && OfflineCrossPromoEntryPoint.HideInputEntryPoint(gameObject, m_event))
                return;
            ControlMapping.Subscribe(m_input, OnInput, InputTrigger.Down, -1);
        }

        // 060023a6 (1.10.1 RVA 0x589730)
        private void OnDisable()
        {
            ControlMapping.Unsubscribe(m_input, OnInput, -1);
        }

        // 060023a7 (1.10.1 RVA 0x5897D0): the UIManager is fetched before either container list is read.
        protected virtual void OnInput(float amount)
        {
            if (m_isSuspended) return;
            UIManager uiManager = ProcessManager.GetSystem<UIManager>();
            if (!m_exclusiveContainers.IsNullOrEmpty() && !uiManager.AreExclusivelyOpen(m_exclusiveContainers)) return;
            if (!m_ignoreIfContainerIsOpen.IsNullOrEmpty())
            {
                foreach (UIContainerIdentifier container in m_ignoreIfContainerIsOpen)
                {
                    if (uiManager.IsOpen(container)) return;
                }
            }
            if (m_triggerImmediately) InvokeEvent();
            else CoroutineUtils.OnNextFrame(InvokeEvent);
        }

        // 060023a8 (1.10.1 RVA 0x589A20): the clip is tested before the source.
        private void InvokeEvent()
        {
            m_event.Invoke();
            if (m_audioClipIdentifier != HLAudioClipIdentifier.None && m_audioSourceIdentifier != HLAudioSourceIdentifier.None && !m_audioManagerRef.IsNull())
                m_audioManagerRef.Get().PlayOneShotAtSource(m_audioClipIdentifier, m_audioSourceIdentifier);
        }

        // 060023a9 (1.10.1 RVA 0x589AC0)
        public void Action_PublishUIMessage(UIModernEvent eventId)
        {
            var exchange = ProcessManager.GetSystemAutoCreate<MessageExchangeBoundCallbackArg<UIModernMessage>>();
            exchange.PublishMessage(new UIModernMessage(UIModernEventType.OnClick), eventId);
        }

        // 060023aa (1.10.1 RVA 0x589B60)
        public void Action_Suspend()
        {
            m_isSuspended = true;
        }

        // 060023ab (1.10.1 RVA 0x589B70)
        public void Action_Unsuspend()
        {
            m_isSuspended = false;
        }

        // 060023ac (1.10.1 RVA 0x589B80): m_exclusiveContainers has no initializer.
        public InputEvent()
        {
        }
    }
}
