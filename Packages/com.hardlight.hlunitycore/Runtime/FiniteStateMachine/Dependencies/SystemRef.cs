using System;
using System.Collections;

namespace Hardlight
{
    // Original six fields begin with these compiler-generated event backing
    // fields. Their declaration order also preserves <WaitOnSystem>d__14.
    public class SystemRef<T> : ISystemRef where T : class, ISystem
    {
        public event Action<T> OnSystemStartup;
        public event Action<T> OnSystemShutdown;
        private FastAction<T> m_actionOnSystemValid;
        private readonly string m_systemName;
        private T m_system;
        private ISystem m_isystem;

        // 0x06000e19; ARM64 shared 0xbf3fd4.
        public SystemRef(string systemName, T system)
        {
            m_systemName = systemName;
            m_system = system;
            m_isystem = system;
        }

        // 0x06000e1a..0x06000e1c; ARM64 0xbf4034/0xbf4044/0xbf4054.
        public bool IsNull() => m_isystem == null;
        public bool IsValid() => m_isystem != null;
        public string SystemName() => m_systemName;

        // 0x06000e1d and original <WaitOnSystem>d__14 (0x06000e27..2c).
        public IEnumerator WaitOnSystem()
        {
            while (IsNull()) yield return null;
        }

        // 0x06000e1e; ARM64 shared 0x79924c. The untyped null predicate
        // precedes diagnosis. A failed T2 cast of a valid system returns null.
        // The typed field is reloaded after the diagnostic callback returns.
        public T2 Get<T2>() where T2 : class, ISystem
        {
            if (m_isystem == null)
                HLUnityCore.LogOrThrowException("System '" + m_systemName + "' is null");
            return m_system as T2;
        }

        // 0x06000e1f; ARM64 shared 0x799374.
        public T2 GetSafe<T2>() where T2 : class, ISystem => m_system as T2;

        // 0x06000e20; ARM64 shared 0xbf40d8.
        public bool TryGet(out T system)
        {
            system = GetSafe();
            return system != null;
        }

        // 0x06000e21; ARM64 shared 0x79943c.
        public bool TryGet<T2>(out T2 system) where T2 : class, ISystem
        {
            system = GetSafe<T2>();
            return system != null;
        }

        // 0x06000e22; ARM64 shared 0xbf4114. Typed and untyped references
        // deliberately remain independent; diagnosis reloads the typed field.
        public T Get()
        {
            if (m_isystem == null)
                HLUnityCore.LogOrThrowException("System '" + m_systemName + "' is null");
            return m_system;
        }

        // 0x06000e23; ARM64 shared 0xbf41ac.
        public T GetSafe() => m_system;

        // 0x06000e24; ARM64 shared 0xbf41b4. Incompatible untyped systems
        // still count as valid and pass null to the typed callback.
        public void InvokeOnValid(Action<T> action)
        {
            if (m_isystem != null) action(m_isystem as T);
            else m_actionOnSystemValid += action;
        }

        // 0x06000e25; ARM64 shared 0xbf42a4. Startup runs before the
        // deferred list is re-read; its callback may replace or revoke refs.
        // Exceptions propagate and prevent the final deferred-list clear.
        void ISystemRef.InternalReplaceSystem(ISystem newSystem)
        {
            m_system = newSystem as T;
            m_isystem = newSystem;
            OnSystemStartup?.Invoke(m_system);
            if (m_actionOnSystemValid != null) m_actionOnSystemValid.Invoke(m_system);
            m_actionOnSystemValid = null;
        }

        // 0x06000e26; ARM64 shared 0xbf4434. Notify before clearing refs.
        void ISystemRef.InternalRevokeSystem()
        {
            if (m_isystem != null) OnSystemShutdown?.Invoke(m_system);
            m_system = null;
            m_isystem = null;
        }
    }

    public class SystemRef : SystemRef<ISystem>
    {
        // 0x06000e2d; ARM64 0x1b18758. Existing base-only context, no new credit.
        public SystemRef(string systemName, ISystem system) : base(systemName, system) { }
    }
}
