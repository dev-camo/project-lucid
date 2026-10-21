using System;
using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 02000834; bodies 0x607950..0x607E00 (1.10.1 x86-64).
    // Relays the hub-interaction string messages to UnityEvents and a bindable label.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ButtonBinaryStateMessageListener : MonoBehaviour
    {
        [SerializeField]
        private ButtonBinaryStateMessageData.ButtonSubscribedToBinaryStateMessage m_buttonSubscribedToBinaryStateMessage;
        [SerializeField]
        private UnityEvent m_onMessageTrue;
        [SerializeField]
        private UnityEvent m_onMessageFalse;
        private SystemRef<MessageManager> m_messageManagerRef;
        public readonly Bindable<string> CurrentText = new Bindable<string>();

        // 06002f9a
        private void Awake()
        {
            m_messageManagerRef = ProcessManager.GetSystemRef<MessageManager>(null, true);
            m_messageManagerRef.InvokeOnValid(SubscribeToMessages);
        }

        // 06002f9b: unsubscribes with fresh delegates, which compare equal to the ones
        // SubscribeToMessages registered.
        private void OnDestroy()
        {
            if (m_messageManagerRef.IsNull())
            {
                return;
            }
            MessageManager messageManager = m_messageManagerRef.Get();
            messageManager.StringMessagesWithCompletion.UnsubscribeFromMessage<ButtonBinaryStateMessageData>(MessageNameKeys.OnHubInteractionStateChanged, HandleStatusMessageData);
            messageManager.StringMessagesWithCompletion.UnsubscribeFromMessage<SimpleStringMessageData>(MessageNameKeys.OnHubInteractionTextChanged, HandleTextMessageData);
            m_messageManagerRef.OnSystemStartup -= SubscribeToMessages;
        }

        // 06002f9c
        private void SubscribeToMessages(MessageManager messageManager)
        {
            messageManager.StringMessagesWithCompletion.SubscribeToMessage<ButtonBinaryStateMessageData>(MessageNameKeys.OnHubInteractionStateChanged, HandleStatusMessageData);
            messageManager.StringMessagesWithCompletion.SubscribeToMessage<SimpleStringMessageData>(MessageNameKeys.OnHubInteractionTextChanged, HandleTextMessageData);
        }

        // 06002f9d: the completion callback is never invoked.
        private void HandleStatusMessageData(in ButtonBinaryStateMessageData messageData, Action completed)
        {
            if (messageData.ButtonWithBinaryState != m_buttonSubscribedToBinaryStateMessage)
            {
                return;
            }
            if (messageData.IsActive)
            {
                m_onMessageTrue?.Invoke();
            }
            else
            {
                m_onMessageFalse?.Invoke();
            }
        }

        // 06002f9e: the completion callback is never invoked.
        private void HandleTextMessageData(in SimpleStringMessageData messageData, Action completed)
        {
            CurrentText.Value = messageData.TextString;
        }
    }
}
