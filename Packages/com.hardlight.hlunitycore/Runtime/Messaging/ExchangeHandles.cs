using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class ExchangeHandleBase<TDelegate> : IExchangeHandle where TDelegate : Delegate
    {
        // 06000bf2/06000bf3; auto-property precedes the original list field.
        public bool Valid { get; private set; } = true;
        private readonly List<TDelegate> m_subscribers = new List<TDelegate>();
        // 06000bf4.
        protected IReadOnlyList<TDelegate> Subscribers => m_subscribers;
        // 06000bf5; clear before invalidation.
        public virtual void Invalidate() { m_subscribers.Clear(); Valid = false; }
        // 06000bf6; null/duplicates are added when valid; invalid null faults before logging.
        public void SubscribeToMessage(TDelegate subscriber)
        {
            if (Valid) m_subscribers.Add(subscriber);
            else HLOutput.LogError(string.Format("Subscribing from an invalid exchange handle: {0} Method: {1} Target: {2}", subscriber, subscriber.Method, subscriber.Target), null);
        }
        // 06000bf7; remove does not inspect Valid.
        public bool UnsubscribeFromMessage(TDelegate subscriber) => m_subscribers.Remove(subscriber);
        // Implicit protected ctor06000bf8 initializes Valid/list before Object.
    }

    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ExchangeHandle : ExchangeHandleBase<MessageCallback>
    {
        // HLUnityCore.Runtime0x06000be6; capture Count once and read live Subscribers on every index.
        public void PublishMessage()
        {
            int count = Subscribers.Count;
            for (int i = 0; i < count; ++i) Subscribers[i]();
        }
        // Implicit original public ctor0x06000be7; genuine handle base only.
    }

    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ExchangeHandle<T> : ExchangeHandleBase<MessageCallback<T>>
    {
        // HLUnityCore.Runtime0x06000be8; capture Count once and read live Subscribers on every index.
        public void PublishMessage(in T obj)
        {
            int count = Subscribers.Count;
            for (int i = 0; i < count; ++i) Subscribers[i](in obj);
        }
        // Implicit original public ctor0x06000be9; genuine handle base only.
    }

    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ExchangeHandle<T1, T2> : ExchangeHandleBase<MessageCallback<T1, T2>>
    {
        // HLUnityCore.Runtime0x06000bea; capture Count once and read live Subscribers on every index.
        public void PublishMessage(in T1 arg1, in T2 arg2)
        {
            int count = Subscribers.Count;
            for (int i = 0; i < count; ++i) Subscribers[i](in arg1, in arg2);
        }
        // Implicit original public ctor0x06000beb; genuine handle base only.
    }

    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ExchangeHandle<T1, T2, T3> : ExchangeHandleBase<MessageCallback<T1, T2, T3>>
    {
        // HLUnityCore.Runtime0x06000bec; capture Count once and read live Subscribers on every index.
        public void PublishMessage(in T1 arg1, in T2 arg2, in T3 arg3)
        {
            int count = Subscribers.Count;
            for (int i = 0; i < count; ++i) Subscribers[i](in arg1, in arg2, in arg3);
        }
        // Implicit original public ctor0x06000bed; genuine handle base only.
    }

    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ExchangeHandle<T1, T2, T3, T4> : ExchangeHandleBase<MessageCallback<T1, T2, T3, T4>>
    {
        // HLUnityCore.Runtime0x06000bee; capture Count once and read live Subscribers on every index.
        public void PublishMessage(in T1 arg1, in T2 arg2, in T3 arg3, T4 arg4)
        {
            int count = Subscribers.Count;
            for (int i = 0; i < count; ++i) Subscribers[i](in arg1, in arg2, in arg3, in arg4);
        }
        // Implicit original public ctor0x06000bef; genuine handle base only.
    }

    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ExchangeHandle<T1, T2, T3, T4, T5> : ExchangeHandleBase<MessageCallback<T1, T2, T3, T4, T5>>
    {
        // HLUnityCore.Runtime0x06000bf0; capture Count once and read live Subscribers on every index.
        public void PublishMessage(in T1 arg1, in T2 arg2, in T3 arg3, in T4 arg4, in T5 arg5)
        {
            int count = Subscribers.Count;
            for (int i = 0; i < count; ++i) Subscribers[i](in arg1, in arg2, in arg3, in arg4, in arg5);
        }
        // Implicit original public ctor0x06000bf1; genuine handle base only.
    }

}
