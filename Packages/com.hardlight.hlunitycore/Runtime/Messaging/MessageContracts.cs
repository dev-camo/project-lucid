using System;

namespace Hardlight
{
    public delegate void MessageCallback();
    public delegate void MessageCallback<T>(in T obj);
    public delegate void MessageCallback<T1, T2>(in T1 arg1, in T2 arg2);
    public delegate void MessageCallback<T1, T2, T3>(in T1 arg1, in T2 arg2, in T3 arg3);
    public delegate void MessageCallback<T1, T2, T3, T4>(in T1 arg1, in T2 arg2, in T3 arg3, in T4 arg4);
    public delegate void MessageCallback<T1, T2, T3, T4, T5>(in T1 arg1, in T2 arg2, in T3 arg3, in T4 arg4, in T5 arg5);

    public interface IExchangeHandle
    {
        bool Valid { get; }
        void Invalidate();
    }

    public readonly struct SubscribeHandle<T>
    {
        private readonly T m_callback;
        // HLUnityCore.Runtime06000c2c: direct callback field read.
        public static implicit operator T(SubscribeHandle<T> handle) => handle.m_callback;
        // 06000c2d: direct callback store.
        public SubscribeHandle(T callback) { m_callback = callback; }
    }
}
