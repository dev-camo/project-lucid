using System;
using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000952; bodies translated from the 1.10.1 x86-64 binary.
    // Mirrors the current character's targeting, surface detector and burst attack messages into
    // bindables for the UI. The completion callbacks are answered at once.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UITargetingListener : MonoBehaviour
    {
        public readonly Bindable<HomingTargetUpdateMessage> TargetingActiveData = new Bindable<HomingTargetUpdateMessage>();
        public readonly Bindable<SurfaceDetectorUpdateMessage> DetectorActiveData = new Bindable<SurfaceDetectorUpdateMessage>();
        public readonly Bindable<BurstAttackUpdateMessage> BurstAttackActiveData = new Bindable<BurstAttackUpdateMessage>();
        private readonly SystemRef<MessageManager> m_messageManagerRef = ProcessManager.GetSystemRef<MessageManager>(null, true);
        private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>(null, true);

        // 0x06003594: follows the character now and on every change.
        private void Start()
        {
            m_characterManagerRef.Get().RegisterOnCharacterChange(OnCharacterChange, true);
        }

        // 0x06003595
        private void OnDestroy()
        {
            UnsubscribeCharacterMessages();
            if (m_characterManagerRef.IsNull())
                return;
            m_characterManagerRef.Get().ReleaseOnCharacterChange(OnCharacterChange);
        }

        // 0x06003596
        private void OnTargetingActive(in HomingTargetUpdateMessage data, Action completed)
        {
            TargetingActiveData.Value = data;
            completed?.Invoke();
        }

        // 0x06003597
        private void OnDetectorActive(in SurfaceDetectorUpdateMessage data, Action completed)
        {
            DetectorActiveData.Value = data;
            completed?.Invoke();
        }

        // 0x06003598
        private void OnBurstAttackActive(in BurstAttackUpdateMessage data, Action completed)
        {
            BurstAttackActiveData.Value = data;
            completed?.Invoke();
        }

        // 0x06003599: the targeting and detector data are cleared (the burst attack data is not),
        // the previous subscriptions are dropped and the three messages are subscribed on the new
        // character.
        private void OnCharacterChange(Character character)
        {
            TargetingActiveData.Value = default;
            DetectorActiveData.Value = default;
            UnsubscribeCharacterMessages();
            MessageManager messageManager = m_messageManagerRef.Get();
            messageManager.ComponentMessagesWithCompletion.SubscribeToMessage(
                character, new MessageCallbackWithCompletion<HomingTargetUpdateMessage>(OnTargetingActive));
            messageManager.ComponentMessagesWithCompletion.SubscribeToMessage(
                character, new MessageCallbackWithCompletion<SurfaceDetectorUpdateMessage>(OnDetectorActive));
            messageManager.ComponentMessagesWithCompletion.SubscribeToMessage(
                character, new MessageCallbackWithCompletion<BurstAttackUpdateMessage>(OnBurstAttackActive));
        }

        // 0x0600359a: nothing is done unless both managers exist and there is a current character.
        private void UnsubscribeCharacterMessages()
        {
            if (m_characterManagerRef.IsNull() || m_messageManagerRef.IsNull())
                return;
            if (!m_characterManagerRef.Get().TryGetCurrentCharacter(out Character character))
                return;
            MessageManager messageManager = m_messageManagerRef.Get();
            messageManager.ComponentMessagesWithCompletion.UnsubscribeFromMessage(
                character, new MessageCallbackWithCompletion<HomingTargetUpdateMessage>(OnTargetingActive));
            messageManager.ComponentMessagesWithCompletion.UnsubscribeFromMessage(
                character, new MessageCallbackWithCompletion<SurfaceDetectorUpdateMessage>(OnDetectorActive));
            messageManager.ComponentMessagesWithCompletion.UnsubscribeFromMessage(
                character, new MessageCallbackWithCompletion<BurstAttackUpdateMessage>(OnBurstAttackActive));
        }

        // 0x0600359b: the field initialisers precede the MonoBehaviour constructor.
        public UITargetingListener() { }
    }
}
