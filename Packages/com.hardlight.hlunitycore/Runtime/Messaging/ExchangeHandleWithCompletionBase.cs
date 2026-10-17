using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x020001b8.
    // The nested types are declared ahead of TryGetCompletionData / SetData so
    // the original closure and state-machine names (<>c__DisplayClass8_0,
    // CompletionData's <>c__DisplayClass9_0/12_0, <RunTimeout>d__11,
    // <Timeout>d__14) get the same member ordinals; their exact position
    // relative to the fields is not recoverable.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class ExchangeHandleWithCompletionBase<TDelegate> : ExchangeHandleBase<TDelegate> where TDelegate : Delegate
    {
        // Original 0x020001b9; a protected nested interface.
        protected interface ICompletionData
        {
            void RunTimeout(int timeoutMs);
            Action GetSubscriberCompletedCallback(int subscriberIndex);
        }

        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        private class CompletionData : ICompletionData
        {
            [Il2CppSetOption(Option.NullChecks, false)]
            [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
            private class CompletionDataDebug
            {
                private readonly List<Delegate> m_subscribers = new List<Delegate>();
                // 0x06000c1c. Clear, then AddRange of the live collection.
                public void Setup(IReadOnlyCollection<Delegate> subscribers)
                {
                    m_subscribers.Clear();
                    m_subscribers.AddRange(subscribers);
                }
                // 0x06000c1d. Unchecked indexer; a fault here precedes the owner's count change.
                public void OnSubscriberFinished(int subscriberIndex) { m_subscribers[subscriberIndex] = null; }
                // 0x06000c1e. The shipped body enumerates and disposes but emits no log calls.
                public void LogTimedOutSubscribers()
                {
                    foreach (Delegate subscriber in m_subscribers) { }
                }
                // Implicit public ctor 0x06000c1f; the List initializer precedes Object's constructor.
            }

            private int m_id;
            private int m_subscriberCount;
            private Action m_subscriberCallback;
            private Action m_finishedCallback;
            private bool m_complete;
            private int m_finishedCount;
            private readonly CompletionDataDebug m_completionDataDebug;

            // 0x06000c14. Object's constructor precedes the optional debug allocation.
            public CompletionData(bool includeDebugInformation)
            {
                if (includeDebugInformation) m_completionDataDebug = new CompletionDataDebug();
            }

            // 0x06000c15; closure 0x06000c22/0x06000c23. Count and optional debug setup precede callback replacement.
            public void SetData(int id, IReadOnlyCollection<Delegate> subscribers, Action finishedCallback)
            {
                m_id = id;
                m_finishedCount = 0;
                m_complete = false;
                m_subscriberCount = subscribers.Count;
                m_finishedCallback = finishedCallback;
                m_completionDataDebug?.Setup(subscribers);
                m_subscriberCallback = () => OnSubscriberFinished(id, -1);
            }

            // 0x06000c16. Leaves the callback, count and id fields in place.
            public void Invalidate() { m_complete = true; }

            // 0x06000c17; async void state machine 0x06000c24/0x06000c25. Every negative timeout skips the delay.
            public async void RunTimeout(int timeoutMs)
            {
                if (timeoutMs >= 0 && !m_complete) await Timeout(timeoutMs);
            }

            // 0x06000c18; closure 0x06000c20/0x06000c21. The debug path captures the current id and the actual subscriber index.
            public Action GetSubscriberCompletedCallback(int subscriberIndex)
            {
                if (m_completionDataDebug != null)
                {
                    int id = m_id;
                    return () => OnSubscriberFinished(id, subscriberIndex);
                }
                return m_subscriberCallback;
            }

            // 0x06000c19. Duplicate callbacks count again; the debug write precedes the count change.
            private void OnSubscriberFinished(int id, int subscriberIndex)
            {
                if (m_complete || m_id != id) return;
                m_completionDataDebug?.OnSubscriberFinished(subscriberIndex);
                m_finishedCount = unchecked(m_finishedCount + 1);
                m_complete = m_finishedCount == m_subscriberCount;
                if (m_complete) m_finishedCallback();
            }

            // 0x06000c1a; async Task state machine 0x06000c26/0x06000c27. The current id is captured before the await; stale data is ignored.
            private async Task Timeout(int timeoutMs)
            {
                int id = m_id;
                await Task.Delay(timeoutMs);
                if (IsComplete(id)) return;
                m_completionDataDebug?.LogTimedOutSubscribers();
                m_complete = true;
                m_finishedCallback();
            }

            // 0x06000c1b. m_complete short-circuits the id read.
            private bool IsComplete(int id) => m_complete || m_id != id;
        }

        private int m_nextMessageID;
        private readonly Stack<CompletionData> m_completionDataPool = new Stack<CompletionData>();
        private readonly Dictionary<int, CompletionData> m_completionDataLookup = new Dictionary<int, CompletionData>();
        private bool m_includeDebugInformation;

        // 0x06000c0d. Changes future allocations only; pooled data keeps its debug choice.
        public void EnableDebugInformation(bool enabled) { m_includeDebugInformation = enabled; }

        // 0x06000c0e. Base invalidation precedes enumerating the live lookup, then the lookup and pool are cleared.
        public override void Invalidate()
        {
            base.Invalidate();
            foreach (CompletionData data in m_completionDataLookup.Values) data.Invalidate();
            m_completionDataLookup.Clear();
            m_completionDataPool.Clear();
        }

        // 0x06000c0f; closure 0x06000c28/0x06000c29. With no subscribers the callback runs before the out value is set to null.
        protected bool TryGetCompletionData(Action finishedCallback, out ICompletionData completionData)
        {
            if (Subscribers.Count == 0)
            {
                finishedCallback();
                completionData = null;
                return false;
            }
            CompletionData newCompletionData = GetCompletionData();
            completionData = newCompletionData;
            int messageID = m_nextMessageID;
            m_completionDataLookup[messageID] = newCompletionData;
            newCompletionData.SetData(messageID, Subscribers, () =>
            {
                m_completionDataLookup.Remove(messageID);
                m_completionDataPool.Push(newCompletionData);
                finishedCallback();
            });
            m_nextMessageID = unchecked(m_nextMessageID + 1);
            return true;
        }

        // 0x06000c10. Pops an existing object without changing its readonly debug record.
        private CompletionData GetCompletionData()
        {
            if (m_completionDataPool.Count > 0) return m_completionDataPool.Pop();
            return new CompletionData(m_includeDebugInformation);
        }
        // Implicit protected ctor 0x06000c11; the Stack and Dictionary initializers precede the base constructor.
    }
}
