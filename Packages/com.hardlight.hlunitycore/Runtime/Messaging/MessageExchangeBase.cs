using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class MessageExchangeBase<TMessage> : ISystem
    {
        private readonly struct MessageHash<TCallback> : IEquatable<MessageHash<TCallback>> where TCallback : Delegate
        {
            // HLUnityCore.Runtime06000bb2; declaration initializers retain BeforeFieldInit.
            public static readonly Type CallbackType = typeof(TCallback);
            private static readonly int s_callbackTypeHashCode = typeof(TCallback).GetHashCode();
            private readonly int m_messageHash;
            // 06000baf; ARM17d01ac: comparer hash before callback type hash XOR.
            public MessageHash(in TMessage message) { m_messageHash = s_messageComparer.GetHashCode(message) ^ s_callbackTypeHashCode; }
            // 06000bb0: only the stored integer participates in this comparison.
            public bool Equals(MessageHash<TCallback> other) => m_messageHash.Equals(other.m_messageHash);
            // 06000bb1.
            public override int GetHashCode() => m_messageHash;
        }

        private readonly struct MessageHashInfo : IDisposable
        {
            private readonly GCHandle m_gcHandle;
            private readonly TMessage m_message;
            private readonly Type m_callbackType;
            // 06000bb3. Readonly GCHandle accesses operate on copies.
            public bool Valid => !m_gcHandle.IsAllocated || m_gcHandle.Target != null;
            // 06000bb4; declared type determines weak storage, with a string exclusion.
            public MessageHashInfo(in TMessage message, Type callbackType)
            {
                m_callbackType = callbackType;
                Type messageType = typeof(TMessage);
                if (messageType.IsClass && messageType != typeof(string))
                {
                    m_message = default;
                    m_gcHandle = GCHandle.Alloc(message, GCHandleType.Weak);
                }
                else
                {
                    m_gcHandle = default;
                    m_message = message;
                }
            }
            // 06000bb5. Validate this temporary only; the weak branch uses Object.Equals.
            public bool Equals(MessageHashInfo other)
            {
                if (m_callbackType != other.m_callbackType) return false;
                if (!Valid) return false;
                if (m_gcHandle.IsAllocated) return m_gcHandle.Target.Equals(other.m_gcHandle.Target);
                return s_messageComparer.Equals(m_message, other.m_message);
            }
            // 06000bb6. The original readonly field is not cleared after Free.
            public void Dispose() { if (m_gcHandle.IsAllocated) m_gcHandle.Free(); }
        }

        private readonly struct MessageInfo
        {
            public readonly MessageHashInfo MessageHashInfo;
            public readonly IExchangeHandle ExchangeHandle;
            // 06000bb7: handle validity precedes message validity.
            public bool Valid => ExchangeHandle.Valid && MessageHashInfo.Valid;
            // 06000bb8.
            public MessageInfo(IExchangeHandle exchangeHandle, MessageHashInfo messageHashInfo)
            {
                MessageHashInfo = messageHashInfo;
                ExchangeHandle = exchangeHandle;
            }
            // 06000bb9: invalidate before disposing the weak reference.
            public void Release() { ExchangeHandle.Invalidate(); MessageHashInfo.Dispose(); }
        }

        // 06000bae; original static field initializer and BeforeFieldInit.
        private static readonly EqualityComparer<TMessage> s_messageComparer = EqualityComparer<TMessage>.Default;
        private readonly List<int> m_invalidMessageHashes = new List<int>(0);
        private readonly Dictionary<int, MessageInfo> m_messageInfos = new Dictionary<int, MessageInfo>();

        // 06000baa; List/Dictionary initializers precede Object construction; only Shutdown is subscribed.
        protected MessageExchangeBase() { this.SubscribeToAction(SystemAction.Shutdown, OnShutdown); }

        // 06000bab: retained invalid keys are deliberately neither cleared nor deduplicated.
        public void ReleaseCollectedMessageInfos()
        {
            foreach (KeyValuePair<int, MessageInfo> entry in m_messageInfos)
            {
                if (!entry.Value.Valid)
                {
                    m_invalidMessageHashes.Add(entry.Key);
                    entry.Value.Release();
                }
            }
            foreach (int hash in m_invalidMessageHashes) m_messageInfos.Remove(hash);
        }

        // 06000bac; live Values enumeration; failure skips Clear and no subscription is removed.
        protected virtual void OnShutdown(object context = null)
        {
            foreach (MessageInfo info in m_messageInfos.Values) info.Release();
            m_messageInfos.Clear();
        }

        // 06000bad; temporary weak handle is made before lookup. Preserve unchecked collision probing,
        // checked cached cast, new() and indexer assignment, including constructor reentrancy.
        protected virtual TExchangeHandle GetExchangeHandleInternal<TCallback, TExchangeHandle>(in TMessage message)
            where TCallback : Delegate where TExchangeHandle : class, IExchangeHandle, new()
        {
            int hash = new MessageHash<TCallback>(in message).GetHashCode();
            var temporary = new MessageHashInfo(in message, MessageHash<TCallback>.CallbackType);
            while (m_messageInfos.TryGetValue(hash, out MessageInfo existing))
            {
                if (temporary.Equals(existing.MessageHashInfo))
                {
                    temporary.Dispose();
                    return (TExchangeHandle)existing.ExchangeHandle;
                }
                hash = unchecked(hash + 1);
            }
            var handle = new TExchangeHandle();
            m_messageInfos[hash] = new MessageInfo(handle, temporary);
            return handle;
        }
    }
}
