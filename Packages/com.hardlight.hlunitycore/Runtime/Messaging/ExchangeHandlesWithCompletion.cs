using System;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original0x020001b2: full PublishMessage and implicit public constructor.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ExchangeHandleWithCompletion : ExchangeHandleWithCompletionBase<MessageCallbackWithCompletion>
    {
        // Original0x06000c01: capture Count once; read live Subscribers/index, then callback; timeout runs last.
        public void PublishMessage(Action finishedCallback, int timeoutMs = -1)
        {
            if (!TryGetCompletionData(finishedCallback, out ICompletionData completionData)) return;
            int count = Subscribers.Count;
            for (int i = 0; i < count; ++i)
                Subscribers[i](completionData.GetSubscriberCompletedCallback(i));
            completionData.RunTimeout(timeoutMs);
        }
        // Implicit original0x06000c02 calls the genuine completion base.
    }

    // Original0x020001b3: full PublishMessage and implicit public constructor.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ExchangeHandleWithCompletion<T> : ExchangeHandleWithCompletionBase<MessageCallbackWithCompletion<T>>
    {
        // Original0x06000c03: capture Count once; read live Subscribers/index, then callback; timeout runs last.
        public void PublishMessage(in T obj, Action finishedCallback, int timeoutMs = -1)
        {
            if (!TryGetCompletionData(finishedCallback, out ICompletionData completionData)) return;
            int count = Subscribers.Count;
            for (int i = 0; i < count; ++i)
                Subscribers[i](in obj, completionData.GetSubscriberCompletedCallback(i));
            completionData.RunTimeout(timeoutMs);
        }
        // Implicit original0x06000c04 calls the genuine completion base.
    }

    // Original0x020001b4: full PublishMessage and implicit public constructor.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ExchangeHandleWithCompletion<T1, T2> : ExchangeHandleWithCompletionBase<MessageCallbackWithCompletion<T1, T2>>
    {
        // Original0x06000c05: capture Count once; read live Subscribers/index, then callback; timeout runs last.
        public void PublishMessage(in T1 arg1, in T2 arg2, Action finishedCallback, int timeoutMs = -1)
        {
            if (!TryGetCompletionData(finishedCallback, out ICompletionData completionData)) return;
            int count = Subscribers.Count;
            for (int i = 0; i < count; ++i)
                Subscribers[i](in arg1, in arg2, completionData.GetSubscriberCompletedCallback(i));
            completionData.RunTimeout(timeoutMs);
        }
        // Implicit original0x06000c06 calls the genuine completion base.
    }

    // Original0x020001b5: full PublishMessage and implicit public constructor.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ExchangeHandleWithCompletion<T1, T2, T3> : ExchangeHandleWithCompletionBase<MessageCallbackWithCompletion<T1, T2, T3>>
    {
        // Original0x06000c07: capture Count once; read live Subscribers/index, then callback; timeout runs last.
        public void PublishMessage(in T1 arg1, in T2 arg2, in T3 arg3, Action finishedCallback, int timeoutMs = -1)
        {
            if (!TryGetCompletionData(finishedCallback, out ICompletionData completionData)) return;
            int count = Subscribers.Count;
            for (int i = 0; i < count; ++i)
                Subscribers[i](in arg1, in arg2, in arg3, completionData.GetSubscriberCompletedCallback(i));
            completionData.RunTimeout(timeoutMs);
        }
        // Implicit original0x06000c08 calls the genuine completion base.
    }

    // Original0x020001b6: full PublishMessage and implicit public constructor.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ExchangeHandleWithCompletion<T1, T2, T3, T4> : ExchangeHandleWithCompletionBase<MessageCallbackWithCompletion<T1, T2, T3, T4>>
    {
        // Original0x06000c09: capture Count once; read live Subscribers/index, then callback; timeout runs last.
        public void PublishMessage(in T1 arg1, in T2 arg2, in T3 arg3, in T4 arg4, Action finishedCallback, int timeoutMs = -1)
        {
            if (!TryGetCompletionData(finishedCallback, out ICompletionData completionData)) return;
            int count = Subscribers.Count;
            for (int i = 0; i < count; ++i)
                Subscribers[i](in arg1, in arg2, in arg3, in arg4, completionData.GetSubscriberCompletedCallback(i));
            completionData.RunTimeout(timeoutMs);
        }
        // Implicit original0x06000c0a calls the genuine completion base.
    }

    // Original0x020001b7: full PublishMessage and implicit public constructor.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ExchangeHandleWithCompletion<T1, T2, T3, T4, T5> : ExchangeHandleWithCompletionBase<MessageCallbackWithCompletion<T1, T2, T3, T4, T5>>
    {
        // Original0x06000c0b: capture Count once; read live Subscribers/index, then callback; timeout runs last.
        public void PublishMessage(in T1 arg1, in T2 arg2, in T3 arg3, in T4 arg4, in T5 arg5, Action finishedCallback, int timeoutMs = -1)
        {
            if (!TryGetCompletionData(finishedCallback, out ICompletionData completionData)) return;
            int count = Subscribers.Count;
            for (int i = 0; i < count; ++i)
                Subscribers[i](in arg1, in arg2, in arg3, in arg4, in arg5, completionData.GetSubscriberCompletedCallback(i));
            completionData.RunTimeout(timeoutMs);
        }
        // Implicit original0x06000c0c calls the genuine completion base.
    }

}
