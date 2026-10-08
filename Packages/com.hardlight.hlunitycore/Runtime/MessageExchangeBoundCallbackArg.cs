using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class MessageExchangeBoundCallbackArg<TMessage> : MessageExchangeBase<TMessage>
    {
        // Original 06000bba: subscribe first, then construct the returned callback handle.
        public SubscribeHandle<MessageCallback> SubscribeToMessage(in TMessage message, MessageCallback callback)
        {
            GetExchangeHandle(in message).SubscribeToMessage(callback);
            return new SubscribeHandle<MessageCallback>(callback);
        }

        // Original 06000bbb uses the bound-capable handle's inherited unbound subscription.
        public SubscribeHandle<MessageCallback<T>> SubscribeToMessage<T>(in TMessage message, MessageCallback<T> callback)
        {
            GetExchangeHandle<T>(in message).SubscribeToMessage(callback);
            return new SubscribeHandle<MessageCallback<T>>(callback);
        }

        // Original 06000bbc rereads obj after subscribing, before constructing the return value.
        public SubscribeHandleBoundCallbackArg<T> SubscribeToMessage<T>(in TMessage message, in T obj, MessageCallback<T> callback)
        {
            GetExchangeHandle<T>(in message).SubscribeToMessage(in obj, callback);
            return new SubscribeHandleBoundCallbackArg<T>(callback, obj);
        }

        // Original 06000bbd.
        public void PublishMessage(in TMessage message) => GetExchangeHandle(in message).PublishMessage();

        // Original 06000bbe retains original optional default3 = Both.
        public void PublishMessage<T>(in TMessage message, in T obj, MessageBroadcastType messageType = MessageBroadcastType.Both)
            => GetExchangeHandle<T>(in message).PublishMessage(in obj, messageType);

        // Original 06000bbf/bc0 do not collect invalid messages before lookup.
        public bool UnsubscribeFromMessage(in TMessage message, MessageCallback callback)
            => GetExchangeHandle(in message).UnsubscribeFromMessage(callback);

        public bool UnsubscribeFromMessage<T>(in TMessage message, MessageCallback<T> callback)
            => GetExchangeHandle<T>(in message).UnsubscribeFromMessage(callback);

        // Original 06000bc1.
        public bool UnsubscribeFromMessage<T>(in TMessage message, in T obj, MessageCallback<T> callback)
            => GetExchangeHandle<T>(in message).UnsubscribeFromMessage(in obj, callback);

        // Original 06000bc2 evaluates the receiver, then object conversion, then callback conversion.
        public bool UnsubscribeFromMessage<T>(in TMessage message, in SubscribeHandleBoundCallbackArg<T> subscribeHandle)
            => GetExchangeHandle<T>(in message).UnsubscribeFromMessage((T)subscribeHandle, (MessageCallback<T>)subscribeHandle);

        // Original 06000bc3/bc4 use the genuine virtual cache lookup inherited from the base.
        public ExchangeHandle GetExchangeHandle(in TMessage message)
            => GetExchangeHandleInternal<MessageCallback, ExchangeHandle>(in message);

        public ExchangeHandleBoundCallbackArg<T> GetExchangeHandle<T>(in TMessage message)
            => GetExchangeHandleInternal<MessageCallback<T>, ExchangeHandleBoundCallbackArg<T>>(in message);

        // Implicit original public constructor06000bc5 calls the genuine exchange base.
    }
}
