using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000206. Every count access goes
    // through the auto-property accessors, as in the shared bodies.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class RefCounter<T>
    {
        // 0x06000d60/0x06000d61.
        public int RefCount { get; private set; }
        private T m_reference;

        // 0x06000d62; shared 0xBC8D20. The first acquire stores the factory
        // result before counting; a throwing factory leaves the count at zero.
        public T Acquire()
        {
            if (RefCount == 0) m_reference = OnFirstAcquire();
            RefCount++;
            return m_reference;
        }

        // 0x06000d63; shared 0xBC8E60. Extra releases are ignored; the last
        // release passes the reference, then clears it after the callback.
        public void Release()
        {
            if (RefCount <= 0) return;
            RefCount--;
            if (RefCount == 0)
            {
                OnLastRelease(m_reference);
                m_reference = default;
            }
        }

        // 0x06000d64; shared 0xBC8FC0. The count is zeroed before the callback.
        public void ForceRelease()
        {
            if (RefCount <= 0) return;
            RefCount = 0;
            OnLastRelease(m_reference);
            m_reference = default;
        }

        // 0x06000d65.
        protected abstract T OnFirstAcquire();

        // 0x06000d66.
        protected abstract void OnLastRelease(T acquiredValue);
    }
}
