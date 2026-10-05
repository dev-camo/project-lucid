using System;
using System.Collections;
using System.Threading;

namespace Hardlight
{
    // Original reference lifecycle, including deferred callbacks and coroutine
    // waiting. The engine diagnostic branches remain explicitly unresolved.
    public class SystemRef<T> : ISystemRef where T : class, ISystem
    {
        private readonly string m_systemName;
        private T m_system;
        private ISystem m_isystem;
        private FastAction<T> m_actionOnSystemValid;
        private Action<T> m_OnSystemStartup;
        private Action<T> m_OnSystemShutdown;

        // 0x06000e15..0x06000e18: standard native delegate add/remove accessors.
        public event Action<T> OnSystemStartup
        {
            add
            {
                Action<T> current = m_OnSystemStartup, previous;
                do
                {
                    previous = current;
                    var next = (Action<T>)Delegate.Combine(previous, value);
                    current = Interlocked.CompareExchange(ref m_OnSystemStartup, next, previous);
                } while (!ReferenceEquals(current, previous));
            }
            remove
            {
                Action<T> current = m_OnSystemStartup, previous;
                do
                {
                    previous = current;
                    var next = (Action<T>)Delegate.Remove(previous, value);
                    current = Interlocked.CompareExchange(ref m_OnSystemStartup, next, previous);
                } while (!ReferenceEquals(current, previous));
            }
        }
        public event Action<T> OnSystemShutdown
        {
            add
            {
                Action<T> current = m_OnSystemShutdown, previous;
                do
                {
                    previous = current;
                    var next = (Action<T>)Delegate.Combine(previous, value);
                    current = Interlocked.CompareExchange(ref m_OnSystemShutdown, next, previous);
                } while (!ReferenceEquals(current, previous));
            }
            remove
            {
                Action<T> current = m_OnSystemShutdown, previous;
                do
                {
                    previous = current;
                    var next = (Action<T>)Delegate.Remove(previous, value);
                    current = Interlocked.CompareExchange(ref m_OnSystemShutdown, next, previous);
                } while (!ReferenceEquals(current, previous));
            }
        }

        // 0x06000e19; arm64 generic 0xbf3fd4: direct stores, no notifications.
        public SystemRef(string systemName, T system)
        {
            m_systemName = systemName;
            m_system = system;
            m_isystem = system;
        }

        // 0x06000e1a..0x06000e1c; arm64 0xbf4034, 0xbf4044, 0xbf4054.
        public bool IsNull() => m_isystem == null;
        public bool IsValid() => m_isystem != null;
        public string SystemName() => m_systemName;

        // 0x06000e1d; arm64 0xbf405c; iterator MoveNext 0xb19058.
        // The original yields null repeatedly while the untyped reference is null.
        public IEnumerator WaitOnSystem()
        {
            while (IsNull()) yield return null;
        }

        // 0x06000e1e; arm64 generic 0x79924c. Conversion uses the typed
        // system reference; incompatible types enter the original diagnostic path.
        public T2 Get<T2>() where T2 : class, ISystem
        {
            T2 result = m_system as T2;
            if (result == null)
                throw new NotSupportedException("Original SystemRef typed-get HLUnityCore diagnostic routing is not recovered.");
            return result;
        }
        // 0x06000e1f; arm64 generic 0x799374.
        public T2 GetSafe<T2>() where T2 : class, ISystem => m_system as T2;
        // 0x06000e20: original non-generic TryGet uses GetSafe.
        public bool TryGet(out T system) { system = GetSafe(); return system != null; }
        // 0x06000e21; arm64 generic 0x79943c.
        public bool TryGet<T2>(out T2 system) where T2 : class, ISystem { system = GetSafe<T2>(); return system != null; }

        // 0x06000e22; arm64 generic 0xbf4114.
        public T Get()
        {
            if (IsNull())
                throw new NotSupportedException("Original SystemRef null-get HLUnityCore diagnostic routing is not recovered.");
            return m_system;
        }
        // 0x06000e23; arm64 generic 0xbf41ac: direct typed reference.
        public T GetSafe() => m_system;

        // 0x06000e24; arm64 generic 0xbf41b4. Validity uses the untyped field;
        // a callback on an incompatible typed reference receives null.
        public void InvokeOnValid(Action<T> action)
        {
            if (m_isystem != null) action(m_isystem as T);
            else m_actionOnSystemValid += action;
        }

        // 0x06000e25; arm64 generic 0xbf42a4. Replacement does not send a
        // shutdown event. Startup happens before deferred-valid callbacks, and
        // a callback exception prevents the deferred list from being cleared.
        void ISystemRef.InternalReplaceSystem(ISystem newSystem)
        {
            m_system = newSystem as T;
            m_isystem = newSystem;
            m_OnSystemStartup?.Invoke(m_system);
            if (m_actionOnSystemValid != null) FastAction<T>.Invoke(m_actionOnSystemValid, m_system);
            m_actionOnSystemValid = null;
        }

        // 0x06000e26; arm64 generic 0xbf4434: notify before clearing both refs.
        void ISystemRef.InternalRevokeSystem()
        {
            if (m_isystem != null) m_OnSystemShutdown?.Invoke(m_system);
            m_system = null;
            m_isystem = null;
        }
    }

    public class SystemRef : SystemRef<ISystem>
    {
        // 0x06000e2d: original derived constructor delegates both arguments.
        public SystemRef(string systemName, ISystem system) : base(systemName, system) { }
    }
}
