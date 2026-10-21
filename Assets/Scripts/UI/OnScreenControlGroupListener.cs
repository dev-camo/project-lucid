using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 02000836; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class OnScreenControlGroupListener : MonoBehaviour
    {
        [Tooltip("UI element GameObject will be enabled/disabled if any of these groups are toggled on/off.")]
        [SerializeField] private List<ScreenControlsGroup> m_controlGroups;
        [Tooltip("Script that will be publishing screen control group messages.")]
        [SerializeField] private MonoBehaviour m_subscribeToMessagesOnComponent;
        [SerializeField] private UIContainerOnScreenControls m_uiContainerOnScreenControls;
        [SerializeField] private UnityEvent m_onShowEvent;
        [SerializeField] private UnityEvent m_onHideEvent;
        private SystemRef<MessageManager> m_messageManagerRef;

        // 06002fa4 (1.10.1 RVA 0x607FE0)
        private void Awake()
        {
            m_messageManagerRef = ProcessManager.GetSystemRef<MessageManager>(null, true);
            if (m_subscribeToMessagesOnComponent != null)
                m_messageManagerRef.InvokeOnValid(SubscribeToMessages);
        }

        // 06002fa5 (RVA 0x608110): unsubscribes only while the publisher and the message manager both exist.
        private void OnDestroy()
        {
            if (m_subscribeToMessagesOnComponent == null || m_messageManagerRef.IsNull()) return;
            m_messageManagerRef.Get().ComponentMessagesWithCompletion
                .UnsubscribeFromMessage<ScreenControlGroupMessageData>(m_subscribeToMessagesOnComponent, OnToggleScreenControlGroup);
            m_messageManagerRef.OnSystemStartup -= SubscribeToMessages;
        }

        // 06002fa6 (RVA 0x6082A0)
        private void SubscribeToMessages(MessageManager messageManager)
        {
            messageManager.ComponentMessagesWithCompletion
                .SubscribeToMessage<ScreenControlGroupMessageData>(m_subscribeToMessagesOnComponent, OnToggleScreenControlGroup);
        }

        // 06002fa7 (RVA 0x608340)
        protected void OnToggleScreenControlGroup(in ScreenControlGroupMessageData messageData, Action completed)
        {
            UpdateState(messageData.GroupToEnable, completed);
        }

        // 06002fa8 (RVA 0x608440): without a container (or before it has published) the Default group applies.
        private void OnEnable()
        {
            ScreenControlsGroup group = ScreenControlsGroup.Default;
            if (m_uiContainerOnScreenControls != null)
            {
                ScreenControlGroupMessageData last = m_uiContainerOnScreenControls.GetLastControlGroupMessageData();
                if (last != null) group = last.GroupToEnable;
            }
            UpdateState(group);
        }

        // 06002fa9 (RVA 0x6083C0)
        private void UpdateState(ScreenControlsGroup groupToEnable, Action completed = null)
        {
            if (m_controlGroups.Contains(groupToEnable)) Show(completed);
            else Hide(completed);
        }

        // 06002faa (RVA 0x608510): the event fires first, then the completion callback.
        protected virtual void Show(Action completed)
        {
            m_onShowEvent?.Invoke();
            completed?.Invoke();
        }

        // 06002fab (RVA 0x608550)
        protected virtual void Hide(Action completed)
        {
            m_onHideEvent?.Invoke();
            completed?.Invoke();
        }

        // 06002fac (RVA 0x608590)
        public OnScreenControlGroupListener()
        {
        }
    }
}
