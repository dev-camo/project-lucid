using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original020001b8: all5 owner methods plus full original natural completion graph.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class ExchangeHandleWithCompletionBase<TDelegate> : ExchangeHandleBase<TDelegate> where TDelegate : Delegate
    {
        private int m_nextMessageID;
        private readonly Stack<CompletionData> m_completionDataPool = new Stack<CompletionData>();
        private readonly Dictionary<int, CompletionData> m_completionDataLookup = new Dictionary<int, CompletionData>();
        private bool m_includeDebugInformation;

        // Original06000c0d. Changes future allocations only; pooled data retains its debug choice.
        public void EnableDebugInformation(bool enabled) { m_includeDebugInformation = enabled; }

        // Original06000c0e: base clear/invalidation before live lookup enumeration, then lookup/pool clear.
        public override void Invalidate()
        {
            base.Invalidate();
            foreach (CompletionData data in m_completionDataLookup.Values) data.Invalidate();
            m_completionDataLookup.Clear();
            m_completionDataPool.Clear();
        }

        // Original06000c0f + natural closure06000c28/29. Empty subscriber path invokes callback before out=null.
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

        // Original06000c10; pop an existing object without changing its readonly debug record.
        private CompletionData GetCompletionData()
        {
            if (m_completionDataPool.Count > 0) return m_completionDataPool.Pop();
            return new CompletionData(m_includeDebugInformation);
        }
        // Implicit original protected06000c11: original Stack/Dictionary initializers precede genuine base ctor.

        // Original020001b9, full2 abstract methods. Protected nested interface, not a replacement contract.
        protected interface ICompletionData
        {
            void RunTimeout(int timeoutMs);
            Action GetSubscriberCompletedCallback(int subscriberIndex);
        }

        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        private class CompletionData : ICompletionData
        {
            private int m_id;
            private int m_subscriberCount;
            private Action m_subscriberCallback;
            private Action m_finishedCallback;
            private bool m_complete;
            private int m_finishedCount;
            private readonly CompletionDataDebug m_completionDataDebug;

            // Original06000c14: Object ctor precedes optional debug allocation.
            public CompletionData(bool includeDebugInformation)
            {
                if (includeDebugInformation) m_completionDataDebug = new CompletionDataDebug();
            }

            // Original06000c15 + closure06000c22/23. Count and optional debug setup precede callback replacement.
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

            // Original06000c16 leaves all callback/count/id fields in place.
            public void Invalidate() { m_complete = true; }

            // Original06000c17 + full async-void06000c24/25. Every negative timeout skips the delay.
            public async void RunTimeout(int timeoutMs)
            {
                if (timeoutMs >= 0 && !m_complete) await Timeout(timeoutMs);
            }

            // Original06000c18 + closure06000c20/21: debug path captures current id and actual subscriber index.
            public Action GetSubscriberCompletedCallback(int subscriberIndex)
            {
                if (m_completionDataDebug != null)
                {
                    int id = m_id;
                    return () => OnSubscriberFinished(id, subscriberIndex);
                }
                return m_subscriberCallback;
            }

            // Original06000c19: duplicate callbacks count again; debug write precedes count mutation.
            private void OnSubscriberFinished(int id, int subscriberIndex)
            {
                if (m_complete || m_id != id) return;
                m_completionDataDebug?.OnSubscriberFinished(subscriberIndex);
                m_finishedCount = unchecked(m_finishedCount + 1);
                m_complete = m_finishedCount == m_subscriberCount;
                if (m_complete) m_finishedCallback();
            }

            // Original06000c1a + full async-task06000c26/27: current id captured before await; stale data is ignored.
            private async Task Timeout(int timeoutMs)
            {
                int id = m_id;
                await Task.Delay(timeoutMs);
                if (IsComplete(id)) return;
                m_completionDataDebug?.LogTimedOutSubscribers();
                m_complete = true;
                m_finishedCallback();
            }

            // Original06000c1b: complete short-circuits current id read.
            private bool IsComplete(int id) => m_complete || m_id != id;

            [Il2CppSetOption(Option.NullChecks, false)]
            [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
            private class CompletionDataDebug
            {
                private readonly List<Delegate> m_subscribers = new List<Delegate>();
                // Original06000c1c: live AddRange after Clear.
                public void Setup(IReadOnlyCollection<Delegate> subscribers)
                {
                    m_subscribers.Clear();
                    m_subscribers.AddRange(subscribers);
                }
                // Original06000c1d: unchecked source indexer, fault occurs before owner count mutation.
                public void OnSubscriberFinished(int subscriberIndex) { m_subscribers[subscriberIndex] = null; }
                // Original06000c1e: shipped body retains enumeration/disposal, with no emitted log calls.
                public void LogTimedOutSubscribers()
                {
                    foreach (Delegate subscriber in m_subscribers) { }
                }
                // Implicit original public06000c1f; List initializer precedes Object ctor.
            }
        }
    }
}
