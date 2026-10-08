using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ExchangeHandleBoundCallbackArg<T> : ExchangeHandleBoundCallbackArgBase<MessageCallback<T>, T>
    {
        // Original 06000bf9 reevaluates both list counts on every loop test.
        // Reentrant publication can replace the contents of the shared outer cache.
        public void PublishMessage(in T obj, MessageBroadcastType messageType = MessageBroadcastType.Both)
        {
            var subscriberLists = GetSubscriberList(messageType, in obj);
            for (int i = 0; i < subscriberLists.Count; ++i)
            {
                var subscribers = subscriberLists[i];
                for (int j = 0; j < subscribers.Count; ++j)
                    subscribers[j](in obj);
            }
        }

        // Original implicit public constructor06000bfa calls the genuine bound base.
    }
}
