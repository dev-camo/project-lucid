using System;
using System.Reflection;
using Hardlight;

namespace ProjectLucid.Tests
{
    // Fills a component's own SystemRef through its InternalReplaceSystem, as the registry would, and on
    // release revokes it and puts the startup observers back. The registry row is never touched, as for
    // an unregistered system. The reference must be empty when leased.
    public sealed class SystemRefLease<T> : IDisposable where T : class, ISystem
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly MethodInfo Replace = typeof(SystemRef<T>).GetMethod("Hardlight.ISystemRef.InternalReplaceSystem", Instance);
        private static readonly MethodInfo Revoke = typeof(SystemRef<T>).GetMethod("Hardlight.ISystemRef.InternalRevokeSystem", Instance);
        private static readonly FieldInfo StartupObservers = typeof(SystemRef<T>).GetField("OnSystemStartup", Instance);

        private readonly SystemRef<T> m_reference;
        private readonly object m_priorObservers;
        private bool m_released;

        public SystemRefLease(SystemRef<T> reference, T system)
        {
            if (reference.IsValid()) throw new InvalidOperationException("Fixture requires an empty " + typeof(T).Name + " reference");
            m_reference = reference;
            m_priorObservers = StartupObservers.GetValue(reference);
            Replace.Invoke(reference, new object[] { system });
        }

        public void Dispose()
        {
            if (m_released) return;
            m_released = true;
            if (m_reference.IsValid()) Revoke.Invoke(m_reference, null);
            StartupObservers.SetValue(m_reference, m_priorObservers);
        }
    }
}
