using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class MessageExchange<TMessage> : MessageExchangeBase<TMessage>
    {
        // HLUnityCore.Runtime0x06000b91; original ordinary arity0 route, no implicit collection.
        public SubscribeHandle<MessageCallback> SubscribeToMessage(in TMessage message, MessageCallback callback)
        {
            GetExchangeHandle(in message).SubscribeToMessage(callback);
            return new SubscribeHandle<MessageCallback>(callback);
        }

        // HLUnityCore.Runtime0x06000b92; original ordinary arity1 route, no implicit collection.
        public SubscribeHandle<MessageCallback<T>> SubscribeToMessage<T>(in TMessage message, MessageCallback<T> callback)
        {
            GetExchangeHandle<T>(in message).SubscribeToMessage(callback);
            return new SubscribeHandle<MessageCallback<T>>(callback);
        }

        // HLUnityCore.Runtime0x06000b93; original ordinary arity2 route, no implicit collection.
        public SubscribeHandle<MessageCallback<T1, T2>> SubscribeToMessage<T1, T2>(in TMessage message, MessageCallback<T1, T2> callback)
        {
            GetExchangeHandle<T1, T2>(in message).SubscribeToMessage(callback);
            return new SubscribeHandle<MessageCallback<T1, T2>>(callback);
        }

        // HLUnityCore.Runtime0x06000b94; original ordinary arity3 route, no implicit collection.
        public SubscribeHandle<MessageCallback<T1, T2, T3>> SubscribeToMessage<T1, T2, T3>(in TMessage message, MessageCallback<T1, T2, T3> callback)
        {
            GetExchangeHandle<T1, T2, T3>(in message).SubscribeToMessage(callback);
            return new SubscribeHandle<MessageCallback<T1, T2, T3>>(callback);
        }

        // HLUnityCore.Runtime0x06000b95; original ordinary arity4 route, no implicit collection.
        public SubscribeHandle<MessageCallback<T1, T2, T3, T4>> SubscribeToMessage<T1, T2, T3, T4>(in TMessage message, MessageCallback<T1, T2, T3, T4> callback)
        {
            GetExchangeHandle<T1, T2, T3, T4>(in message).SubscribeToMessage(callback);
            return new SubscribeHandle<MessageCallback<T1, T2, T3, T4>>(callback);
        }

        // HLUnityCore.Runtime0x06000b96; original ordinary arity5 route, no implicit collection.
        public SubscribeHandle<MessageCallback<T1, T2, T3, T4, T5>> SubscribeToMessage<T1, T2, T3, T4, T5>(in TMessage message, MessageCallback<T1, T2, T3, T4, T5> callback)
        {
            GetExchangeHandle<T1, T2, T3, T4, T5>(in message).SubscribeToMessage(callback);
            return new SubscribeHandle<MessageCallback<T1, T2, T3, T4, T5>>(callback);
        }

        // HLUnityCore.Runtime0x06000b97; original ordinary arity0 route, no implicit collection.
        public void PublishMessage(in TMessage message) => GetExchangeHandle(in message).PublishMessage();

        // HLUnityCore.Runtime0x06000b98; original ordinary arity1 route, no implicit collection.
        public void PublishMessage<T>(in TMessage message, in T obj) => GetExchangeHandle<T>(in message).PublishMessage(in obj);

        // HLUnityCore.Runtime0x06000b99; original ordinary arity2 route, no implicit collection.
        public void PublishMessage<T1, T2>(in TMessage message, in T1 arg1, in T2 arg2) => GetExchangeHandle<T1, T2>(in message).PublishMessage(in arg1, in arg2);

        // HLUnityCore.Runtime0x06000b9a; original ordinary arity3 route, no implicit collection.
        public void PublishMessage<T1, T2, T3>(in TMessage message, in T1 arg1, in T2 arg2, in T3 arg3) => GetExchangeHandle<T1, T2, T3>(in message).PublishMessage(in arg1, in arg2, in arg3);

        // HLUnityCore.Runtime0x06000b9b; original ordinary arity4 route, no implicit collection.
        public void PublishMessage<T1, T2, T3, T4>(in TMessage message, in T1 arg1, in T2 arg2, in T3 arg3, in T4 arg4) => GetExchangeHandle<T1, T2, T3, T4>(in message).PublishMessage(in arg1, in arg2, in arg3, arg4);

        // HLUnityCore.Runtime0x06000b9c; original ordinary arity5 route, no implicit collection.
        public void PublishMessage<T1, T2, T3, T4, T5>(in TMessage message, in T1 arg1, in T2 arg2, in T3 arg3, in T4 arg4, in T5 arg5) => GetExchangeHandle<T1, T2, T3, T4, T5>(in message).PublishMessage(in arg1, in arg2, in arg3, in arg4, in arg5);

        // HLUnityCore.Runtime0x06000b9d; original ordinary arity0 route, no implicit collection.
        public bool UnsubscribeFromMessage(in TMessage message, MessageCallback callback) => GetExchangeHandle(in message).UnsubscribeFromMessage(callback);

        // HLUnityCore.Runtime0x06000b9e; original ordinary arity1 route, no implicit collection.
        public bool UnsubscribeFromMessage<T>(in TMessage message, MessageCallback<T> callback) => GetExchangeHandle<T>(in message).UnsubscribeFromMessage(callback);

        // HLUnityCore.Runtime0x06000b9f; original ordinary arity2 route, no implicit collection.
        public bool UnsubscribeFromMessage<T1, T2>(in TMessage message, MessageCallback<T1, T2> callback) => GetExchangeHandle<T1, T2>(in message).UnsubscribeFromMessage(callback);

        // HLUnityCore.Runtime0x06000ba0; original ordinary arity3 route, no implicit collection.
        public bool UnsubscribeFromMessage<T1, T2, T3>(in TMessage message, MessageCallback<T1, T2, T3> callback) => GetExchangeHandle<T1, T2, T3>(in message).UnsubscribeFromMessage(callback);

        // HLUnityCore.Runtime0x06000ba1; original ordinary arity4 route, no implicit collection.
        public bool UnsubscribeFromMessage<T1, T2, T3, T4>(in TMessage message, MessageCallback<T1, T2, T3, T4> callback) => GetExchangeHandle<T1, T2, T3, T4>(in message).UnsubscribeFromMessage(callback);

        // HLUnityCore.Runtime0x06000ba2; original ordinary arity5 route, no implicit collection.
        public bool UnsubscribeFromMessage<T1, T2, T3, T4, T5>(in TMessage message, MessageCallback<T1, T2, T3, T4, T5> callback) => GetExchangeHandle<T1, T2, T3, T4, T5>(in message).UnsubscribeFromMessage(callback);

        // HLUnityCore.Runtime0x06000ba3; original ordinary arity0 route, no implicit collection.
        public ExchangeHandle GetExchangeHandle(in TMessage message) => GetExchangeHandleInternal<MessageCallback, ExchangeHandle>(in message);

        // HLUnityCore.Runtime0x06000ba4; original ordinary arity1 route, no implicit collection.
        public ExchangeHandle<T> GetExchangeHandle<T>(in TMessage message) => GetExchangeHandleInternal<MessageCallback<T>, ExchangeHandle<T>>(in message);

        // HLUnityCore.Runtime0x06000ba5; original ordinary arity2 route, no implicit collection.
        public ExchangeHandle<T1, T2> GetExchangeHandle<T1, T2>(in TMessage message) => GetExchangeHandleInternal<MessageCallback<T1, T2>, ExchangeHandle<T1, T2>>(in message);

        // HLUnityCore.Runtime0x06000ba6; original ordinary arity3 route, no implicit collection.
        public ExchangeHandle<T1, T2, T3> GetExchangeHandle<T1, T2, T3>(in TMessage message) => GetExchangeHandleInternal<MessageCallback<T1, T2, T3>, ExchangeHandle<T1, T2, T3>>(in message);

        // HLUnityCore.Runtime0x06000ba7; original ordinary arity4 route, no implicit collection.
        public ExchangeHandle<T1, T2, T3, T4> GetExchangeHandle<T1, T2, T3, T4>(in TMessage message) => GetExchangeHandleInternal<MessageCallback<T1, T2, T3, T4>, ExchangeHandle<T1, T2, T3, T4>>(in message);

        // HLUnityCore.Runtime0x06000ba8; original ordinary arity5 route, no implicit collection.
        public ExchangeHandle<T1, T2, T3, T4, T5> GetExchangeHandle<T1, T2, T3, T4, T5>(in TMessage message) => GetExchangeHandleInternal<MessageCallback<T1, T2, T3, T4, T5>, ExchangeHandle<T1, T2, T3, T4, T5>>(in message);

        // Implicit original public ctor06000ba9; genuine exchange base only.
    }
}
