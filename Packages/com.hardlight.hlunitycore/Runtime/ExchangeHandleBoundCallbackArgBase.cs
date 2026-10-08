using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class ExchangeHandleBoundCallbackArgBase<TDelegate, TValue> : ExchangeHandleBase<TDelegate>
        where TDelegate : Delegate
    {
        private readonly List<IReadOnlyList<TDelegate>> m_subscriberCache = new List<IReadOnlyList<TDelegate>>();
        private readonly Dictionary<TValue, List<TDelegate>> m_boundCallbackArgSubscribers = new Dictionary<TValue, List<TDelegate>>();

        // Original 06000bfb calls genuine DictionaryExtensions.TryGetOrNew06000266.
        protected IReadOnlyList<TDelegate> SubscribersBoundCallbackArg(TValue obj)
            => m_boundCallbackArgSubscribers.TryGetOrNew(obj);

        // Original 06000bfc clears unbound subscribers, invalidates, then clears the bound dictionary.
        // The shared subscriber-list cache is retained.
        public override void Invalidate()
        {
            base.Invalidate();
            m_boundCallbackArgSubscribers.Clear();
        }

        // Original 06000bfd allows null and duplicate callbacks while valid.
        // On an invalid handle, a null callback faults during argument evaluation before logging.
        public void SubscribeToMessage(in TValue obj, TDelegate subscriber)
        {
            if (Valid)
                m_boundCallbackArgSubscribers.TryGetOrNew(obj).Add(subscriber);
            else
                HLOutput.LogError(string.Format("Subscribing from an invalid exchange handle: {0} Method: {1} Target: {2}",
                    subscriber, subscriber.Method, subscriber.Target), null);
        }

        // Original 06000bfe does not create a missing binding or check Valid.
        public bool UnsubscribeFromMessage(in TValue obj, TDelegate subscriber)
        {
            return m_boundCallbackArgSubscribers.TryGetValue(obj, out var subscribers) && subscribers.Remove(subscriber);
        }

        // Original 06000bff rebuilds one shared cache. The bound path creates a list for a new key,
        // and the cache receiver is evaluated before that lookup; a fault retains the earlier prefix.
        protected IReadOnlyList<IReadOnlyList<TDelegate>> GetSubscriberList(MessageBroadcastType messageBroadcastType, in TValue obj)
        {
            m_subscriberCache.Clear();
            if ((messageBroadcastType & MessageBroadcastType.Unbound) != 0)
                m_subscriberCache.Add(Subscribers);
            if ((messageBroadcastType & MessageBroadcastType.Bound) != 0)
                m_subscriberCache.Add(m_boundCallbackArgSubscribers.TryGetOrNew(obj));
            return m_subscriberCache;
        }

        // Original protected constructor06000c00 initializes both fields before the genuine base.
    }
}
