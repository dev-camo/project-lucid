using System;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original020001a8: all26 methods, one readonly field. Full both-architecture captures retained.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class MessageExchangeWithCompletion<TMessage> : MessageExchangeBase<TMessage>
    {
        private readonly bool m_includeDebugInformation;

        // Original HLUnityCore.Runtime0x06000bcc.
        public MessageExchangeWithCompletion(bool includeDebugInformation = false) { m_includeDebugInformation = includeDebugInformation; }

        // Original HLUnityCore.Runtime0x06000bcd.
        public SubscribeHandle<MessageCallbackWithCompletion> SubscribeToMessage(in TMessage message, MessageCallbackWithCompletion completionCallback)
        {
            GetExchangeHandle(in message).SubscribeToMessage(completionCallback);
            return new SubscribeHandle<MessageCallbackWithCompletion>(completionCallback);
        }

        // Original HLUnityCore.Runtime0x06000bce.
        public SubscribeHandle<MessageCallbackWithCompletion<T>> SubscribeToMessage<T>(in TMessage message, MessageCallbackWithCompletion<T> completionCallback)
        {
            GetExchangeHandle<T>(in message).SubscribeToMessage(completionCallback);
            return new SubscribeHandle<MessageCallbackWithCompletion<T>>(completionCallback);
        }

        // Original HLUnityCore.Runtime0x06000bcf.
        public SubscribeHandle<MessageCallbackWithCompletion<T1, T2>> SubscribeToMessage<T1, T2>(in TMessage message, MessageCallbackWithCompletion<T1, T2> completionCallback)
        {
            GetExchangeHandle<T1, T2>(in message).SubscribeToMessage(completionCallback);
            return new SubscribeHandle<MessageCallbackWithCompletion<T1, T2>>(completionCallback);
        }

        // Original HLUnityCore.Runtime0x06000bd0.
        public SubscribeHandle<MessageCallbackWithCompletion<T1, T2, T3>> SubscribeToMessage<T1, T2, T3>(in TMessage message, MessageCallbackWithCompletion<T1, T2, T3> completionCallback)
        {
            GetExchangeHandle<T1, T2, T3>(in message).SubscribeToMessage(completionCallback);
            return new SubscribeHandle<MessageCallbackWithCompletion<T1, T2, T3>>(completionCallback);
        }

        // Original HLUnityCore.Runtime0x06000bd1.
        public SubscribeHandle<MessageCallbackWithCompletion<T1, T2, T3, T4>> SubscribeToMessage<T1, T2, T3, T4>(in TMessage message, MessageCallbackWithCompletion<T1, T2, T3, T4> completionCallback)
        {
            GetExchangeHandle<T1, T2, T3, T4>(in message).SubscribeToMessage(completionCallback);
            return new SubscribeHandle<MessageCallbackWithCompletion<T1, T2, T3, T4>>(completionCallback);
        }

        // Original HLUnityCore.Runtime0x06000bd2.
        public SubscribeHandle<MessageCallbackWithCompletion<T1, T2, T3, T4, T5>> SubscribeToMessage<T1, T2, T3, T4, T5>(in TMessage message, MessageCallbackWithCompletion<T1, T2, T3, T4, T5> completionCallback)
        {
            GetExchangeHandle<T1, T2, T3, T4, T5>(in message).SubscribeToMessage(completionCallback);
            return new SubscribeHandle<MessageCallbackWithCompletion<T1, T2, T3, T4, T5>>(completionCallback);
        }

        // Original HLUnityCore.Runtime0x06000bd3.
        public void PublishMessage(in TMessage message, Action completionCallback, int timeoutMs = -1) => GetExchangeHandle(in message).PublishMessage(completionCallback, timeoutMs);

        // Original HLUnityCore.Runtime0x06000bd4.
        public void PublishMessage<T>(in TMessage message, in T obj, Action completionCallback, int timeoutMs = -1) => GetExchangeHandle<T>(in message).PublishMessage(in obj, completionCallback, timeoutMs);

        // Original HLUnityCore.Runtime0x06000bd5.
        public void PublishMessage<T1, T2>(in TMessage message, in T1 arg1, in T2 arg2, Action completionCallback, int timeoutMs = -1) => GetExchangeHandle<T1, T2>(in message).PublishMessage(in arg1, in arg2, completionCallback, timeoutMs);

        // Original HLUnityCore.Runtime0x06000bd6.
        public void PublishMessage<T1, T2, T3>(in TMessage message, in T1 arg1, in T2 arg2, in T3 arg3, Action completionCallback, int timeoutMs = -1) => GetExchangeHandle<T1, T2, T3>(in message).PublishMessage(in arg1, in arg2, in arg3, completionCallback, timeoutMs);

        // Original HLUnityCore.Runtime0x06000bd7.
        public void PublishMessage<T1, T2, T3, T4>(in TMessage message, in T1 arg1, in T2 arg2, in T3 arg3, in T4 arg4, Action completionCallback, int timeoutMs = -1) => GetExchangeHandle<T1, T2, T3, T4>(in message).PublishMessage(in arg1, in arg2, in arg3, in arg4, completionCallback, timeoutMs);

        // Original HLUnityCore.Runtime0x06000bd8.
        public void PublishMessage<T1, T2, T3, T4, T5>(in TMessage message, in T1 arg1, in T2 arg2, in T3 arg3, in T4 arg4, in T5 arg5, Action completionCallback, int timeoutMs = -1) => GetExchangeHandle<T1, T2, T3, T4, T5>(in message).PublishMessage(in arg1, in arg2, in arg3, in arg4, in arg5, completionCallback, timeoutMs);

        // Original HLUnityCore.Runtime0x06000bd9.
        public bool UnsubscribeFromMessage(in TMessage message, MessageCallbackWithCompletion completionCallback) => GetExchangeHandle(in message).UnsubscribeFromMessage(completionCallback);

        // Original HLUnityCore.Runtime0x06000bda.
        public bool UnsubscribeFromMessage<T>(in TMessage message, MessageCallbackWithCompletion<T> completionCallback) => GetExchangeHandle<T>(in message).UnsubscribeFromMessage(completionCallback);

        // Original HLUnityCore.Runtime0x06000bdb.
        public bool UnsubscribeFromMessage<T1, T2>(in TMessage message, MessageCallbackWithCompletion<T1, T2> completionCallback) => GetExchangeHandle<T1, T2>(in message).UnsubscribeFromMessage(completionCallback);

        // Original HLUnityCore.Runtime0x06000bdc.
        public bool UnsubscribeFromMessage<T1, T2, T3>(in TMessage message, MessageCallbackWithCompletion<T1, T2, T3> completionCallback) => GetExchangeHandle<T1, T2, T3>(in message).UnsubscribeFromMessage(completionCallback);

        // Original HLUnityCore.Runtime0x06000bdd.
        public bool UnsubscribeFromMessage<T1, T2, T3, T4>(in TMessage message, MessageCallbackWithCompletion<T1, T2, T3, T4> completionCallback) => GetExchangeHandle<T1, T2, T3, T4>(in message).UnsubscribeFromMessage(completionCallback);

        // Original HLUnityCore.Runtime0x06000bde.
        public bool UnsubscribeFromMessage<T1, T2, T3, T4, T5>(in TMessage message, MessageCallbackWithCompletion<T1, T2, T3, T4, T5> completionCallback) => GetExchangeHandle<T1, T2, T3, T4, T5>(in message).UnsubscribeFromMessage(completionCallback);

        // Original HLUnityCore.Runtime0x06000bdf.
        public ExchangeHandleWithCompletion GetExchangeHandle(in TMessage message) => GetExchangeHandleInternal<MessageCallbackWithCompletion, ExchangeHandleWithCompletion>(in message);

        // Original HLUnityCore.Runtime0x06000be0.
        public ExchangeHandleWithCompletion<T> GetExchangeHandle<T>(in TMessage message) => GetExchangeHandleInternal<MessageCallbackWithCompletion<T>, ExchangeHandleWithCompletion<T>>(in message);

        // Original HLUnityCore.Runtime0x06000be1.
        public ExchangeHandleWithCompletion<T1, T2> GetExchangeHandle<T1, T2>(in TMessage message) => GetExchangeHandleInternal<MessageCallbackWithCompletion<T1, T2>, ExchangeHandleWithCompletion<T1, T2>>(in message);

        // Original HLUnityCore.Runtime0x06000be2.
        public ExchangeHandleWithCompletion<T1, T2, T3> GetExchangeHandle<T1, T2, T3>(in TMessage message) => GetExchangeHandleInternal<MessageCallbackWithCompletion<T1, T2, T3>, ExchangeHandleWithCompletion<T1, T2, T3>>(in message);

        // Original HLUnityCore.Runtime0x06000be3.
        public ExchangeHandleWithCompletion<T1, T2, T3, T4> GetExchangeHandle<T1, T2, T3, T4>(in TMessage message) => GetExchangeHandleInternal<MessageCallbackWithCompletion<T1, T2, T3, T4>, ExchangeHandleWithCompletion<T1, T2, T3, T4>>(in message);

        // Original HLUnityCore.Runtime0x06000be4.
        public ExchangeHandleWithCompletion<T1, T2, T3, T4, T5> GetExchangeHandle<T1, T2, T3, T4, T5>(in TMessage message) => GetExchangeHandleInternal<MessageCallbackWithCompletion<T1, T2, T3, T4, T5>, ExchangeHandleWithCompletion<T1, T2, T3, T4, T5>>(in message);

        // Original HLUnityCore.Runtime0x06000be5.
        protected override TExchangeHandle GetExchangeHandleInternal<TCallback, TExchangeHandle>(in TMessage message)
        {
            TExchangeHandle handle = base.GetExchangeHandleInternal<TCallback, TExchangeHandle>(in message);
            if (m_includeDebugInformation && handle is ExchangeHandleWithCompletionBase<TCallback> completionHandle)
                completionHandle.EnableDebugInformation(true);
            return handle;
        }

    }
}
