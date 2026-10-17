using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x020001e4.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class Observable<T> : IObservable<T>
    {
        // 0x06000c5c; shared 0x18C14A0.
        public static implicit operator T(Observable<T> observable) => observable.m_value;

        // 0x06000c5d/0x06000c5e. The empty anonymous method <.ctor>b__6_0
        // (0x06000c64) is cached by the initializer before Object's
        // constructor, so the event is never null unless a caller removes it.
        public event Action<T> OnValueUpdated = delegate { };

        private readonly IEqualityComparer<T> m_comparer;
        private T m_value;

        // 0x06000c5f; shared 0x18C1710. The value is stored before the
        // comparer, which falls back to the default comparer for null.
        public Observable(T defaultValue = default(T), IEqualityComparer<T> comparer = null)
        {
            m_value = defaultValue;
            m_comparer = comparer ?? EqualityComparer<T>.Default;
        }

        // 0x06000c60; shared 0x18C19E0. The comparer receives the current
        // value first; equal values neither store nor notify.
        public void Assign(in T value)
        {
            if (m_comparer.Equals(m_value, value)) return;
            m_value = value;
            ValueUpdated();
        }

        // 0x06000c61; shared 0x18C1BF0. Unconditional notification.
        public void ValueUpdated()
        {
            OnValueUpdated(m_value);
        }
    }
}
