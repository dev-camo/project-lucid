namespace Hardlight
{
    public readonly struct SubscribeHandleBoundCallbackArg<T>
    {
        private readonly MessageCallback<T> m_callback;
        private readonly T m_object;

        // Original 06000c2e and06000c2f read the corresponding retained field.
        public static implicit operator MessageCallback<T>(SubscribeHandleBoundCallbackArg<T> handle) => handle.m_callback;
        public static implicit operator T(SubscribeHandleBoundCallbackArg<T> handle) => handle.m_object;

        // Original 06000c30 writes the callback before the object value.
        public SubscribeHandleBoundCallbackArg(MessageCallback<T> callback, T obj)
        {
            m_callback = callback;
            m_object = obj;
        }
    }
}
