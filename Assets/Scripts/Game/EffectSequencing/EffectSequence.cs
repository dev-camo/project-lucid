using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original0200060b: full sixteen owner methods and genuine closure2095/2096.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class EffectSequence : MonoBehaviour
    {
        [SerializeField] public EffectList[] m_effectSequence = Array.Empty<EffectList>();
        [SerializeField] public Component m_subscribeToMessagesOnComponent;
        private MessageExchangeWithCompletion<int> m_messageExchange;
        private readonly SystemRef<MessageManager> m_messageManager = ProcessManager.GetSystemRef<MessageManager>(null, true);
        private Component m_triggeringComponent;
        private Component m_triggeringComponentToUnsubscribe;
        private Action m_onComplete;
        private Func<bool> m_verifyFunction;

        // Original06002085: capture outer count, retain live outer field and captured inner array.
        private void Awake()
        {
            m_messageExchange = new MessageExchangeWithCompletion<int>();
            int count = m_effectSequence.Length;
            for (int i = 0; i < count; i++)
            {
                SequencedEffect[] effects = m_effectSequence[i].Effects;
                foreach (SequencedEffect effect in effects)
                    m_messageExchange.SubscribeToMessage<IEffectData>(in i, effect.PlayEffect);
            }
            if (m_subscribeToMessagesOnComponent != null) SetTrigger(m_subscribeToMessagesOnComponent);
        }

        // Original06002086: cancel before unsubscription, then private exchange shutdown.
        private void OnDestroy()
        {
            CancelSequence();
            int count = m_effectSequence.Length;
            for (int i = 0; i < count; i++)
            {
                SequencedEffect[] effects = m_effectSequence[i].Effects;
                foreach (SequencedEffect effect in effects)
                    m_messageExchange.UnsubscribeFromMessage<IEffectData>(in i, effect.PlayEffect);
            }
            m_messageExchange.ProcessSystemAction(SystemAction.Shutdown);
            m_messageExchange.UnsubscribeFromAction(SystemAction.Shutdown);
            if (m_messageManager.IsValid())
            {
                MessageManager manager = m_messageManager.Get();
                if (m_subscribeToMessagesOnComponent != null)
                    manager.ComponentMessagesWithCompletion.UnsubscribeFromMessage<IEffectData>(in m_subscribeToMessagesOnComponent, ValidateAndBegin);
                if (m_triggeringComponent != null)
                    manager.ComponentMessagesWithCompletion.UnsubscribeFromMessage<IEffectData>(in m_triggeringComponent, ValidateAndBegin);
            }
        }

        // Original06002087: Unity equality prevents all work for an unchanged trigger.
        public void SetTrigger(Component component)
        {
            if (m_triggeringComponent == component) return;
            m_triggeringComponentToUnsubscribe = m_triggeringComponent;
            m_triggeringComponent = component;
            m_messageManager.InvokeOnValid(OnMessageManagerInit);
        }
        // Original06002088: an equal component also leaves the existing verifier unchanged.
        public void SetTrigger(Component component, Func<bool> verifyFunction)
        {
            if (m_triggeringComponent == component) return;
            m_triggeringComponentToUnsubscribe = m_triggeringComponent;
            m_triggeringComponent = component;
            m_verifyFunction = verifyFunction;
            m_messageManager.InvokeOnValid(OnMessageManagerInit);
        }
        // Original06002089: the shipped unsubscription uses the CURRENT key, despite testing the previous component.
        private void OnMessageManagerInit(MessageManager messageManager)
        {
            if (m_triggeringComponentToUnsubscribe != null)
            {
                messageManager.ComponentMessagesWithCompletion.UnsubscribeFromMessage<IEffectData>(in m_triggeringComponent, ValidateAndBegin);
                m_triggeringComponentToUnsubscribe = null;
            }
            messageManager.ComponentMessagesWithCompletion.SubscribeToMessage<IEffectData>(in m_triggeringComponent, ValidateAndBegin);
        }
        // Original0600208a: rejected verification neither calls completion nor changes the saved callback.
        private void ValidateAndBegin(in IEffectData data, Action onComplete)
        {
            if (m_verifyFunction != null && !m_verifyFunction()) return;
            m_onComplete = onComplete;
            TriggerEffect(data, 0);
        }
        // Original0600208b: direct sequence entry does not consult the verifier.
        public void BeginSequence(in IEffectData data, Action onComplete)
        {
            m_onComplete = onComplete;
            TriggerEffect(data, 0);
        }
        // Original0600208c: retain transform.gameObject.activeInHierarchy getter order.
        public void BeginSequence()
        {
            if (!transform.gameObject.activeInHierarchy) return;
            IEffectData data = new TargetedEffect { Target = transform, Offset = Vector3.zero };
            m_onComplete = null;
            TriggerEffect(data, 0);
        }
        // Original0600208d: collider entry has no hierarchy-active guard.
        public void BeginSequence(Collider collider)
        {
            IEffectData data = new TargetedEffect { Target = collider.transform, Offset = Vector3.zero };
            m_onComplete = null;
            TriggerEffect(data, 0);
        }
        // Original0600208e: deliberately retain the callback, allowing repeated EndSequence calls.
        public void EndSequence() { m_onComplete?.Invoke(); }
        // Original0600208f + actual mutable-index closure2095/2096. Equality is the only terminal index test.
        private void TriggerEffect(IEffectData data, int effectIndex)
        {
            if (effectIndex == m_effectSequence.Length)
            {
                EndSequence();
                return;
            }
            m_messageExchange.PublishMessage<IEffectData>(in effectIndex, in data, () => TriggerEffect(data, ++effectIndex), -1);
        }
        // Original06002090: both arrays and their lengths remain live after each cancellation.
        public void CancelSequence()
        {
            for (int i = 0; i < m_effectSequence.Length; i++)
            {
                m_messageExchange.GetExchangeHandle<IEffectData>(in i).Invalidate();
                for (int j = 0; j < m_effectSequence[i].Effects.Length; j++)
                    m_effectSequence[i].Effects[j].CancelCoroutine();
            }
            m_onComplete = null;
        }
        // Original06002091: Unity-null sequence calls the supplied callback without a null guard.
        public static void BeginSequence(EffectSequence effectSequence, Action onComplete)
        {
            if (effectSequence == null) { onComplete(); return; }
            effectSequence.m_onComplete = onComplete;
            effectSequence.TriggerEffect(null, 0);
        }
        // Original06002092: data is read only after the non-null callback field assignment.
        public static void BeginSequence(EffectSequence effectSequence, in IEffectData data, Action onComplete)
        {
            if (effectSequence == null) { onComplete(); return; }
            effectSequence.m_onComplete = onComplete;
            effectSequence.TriggerEffect(data, 0);
        }
        // Original06002093: start callback precedes end-delegate construction and field replacement.
        public void BeginSequence(ISequencedEffectWithCallback callback)
        {
            IEffectData data = new TargetedEffect { Target = transform, Offset = Vector3.zero };
            callback.OnSequenceStart();
            m_onComplete = callback.OnSequenceEnd;
            TriggerEffect(data, 0);
        }
        // Implicit original06002094: empty authored array and real SystemRef initializer precede MonoBehaviour ctor.
    }
}
