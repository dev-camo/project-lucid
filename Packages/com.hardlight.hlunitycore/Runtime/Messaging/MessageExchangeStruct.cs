using System;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x020001a7. Struct payload types are their
    // own message: the exchange key is typeof(T).
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class MessageExchangeStruct : MessageExchangeBase<Type>
    {
        // 0x06000bc6; shared 0x979660.
        public SubscribeHandle<MessageCallback<T>> SubscribeToMessage<T>(MessageCallback<T> callback) where T : struct
        {
            GetExchangeHandle<T>().SubscribeToMessage(callback);
            return new SubscribeHandle<MessageCallback<T>>(callback);
        }

        // 0x06000bc7; shared 0x979580. Publishes a zeroed payload through the
        // in overload below.
        public void PublishMessage<T>() where T : struct => PublishMessage(default(T));

        // 0x06000bc8; shared 0x979610.
        public void PublishMessage<T>(in T data) where T : struct => GetExchangeHandle<T>().PublishMessage(in data);

        // 0x06000bc9; shared 0x9796D0.
        public bool UnsubscribeFromMessage<T>(MessageCallback<T> callback) where T : struct =>
            GetExchangeHandle<T>().UnsubscribeFromMessage(callback);

        // 0x06000bca; shared 0x9794E0. typeof(T) is passed by reference from a
        // temporary through the virtual generic lookup.
        public ExchangeHandle<T> GetExchangeHandle<T>() where T : struct =>
            GetExchangeHandleInternal<MessageCallback<T>, ExchangeHandle<T>>(typeof(T));

        // Implicit public ctor 0x06000bcb (0x1B05E20); MessageExchangeBase<Type> only.
    }
}
