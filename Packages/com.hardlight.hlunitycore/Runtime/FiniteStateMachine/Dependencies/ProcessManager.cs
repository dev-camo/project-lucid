using System;
using System.Collections.Generic;

namespace Hardlight
{
    // Original metadata contract contains no members.
    public interface ISystem { }
    internal interface ISystemRef
    {
        void InternalReplaceSystem(ISystem newSystem);
        void InternalRevokeSystem();
    }

    // Partial original registry API. Engine system-action dispatch, enumeration
    // and unregistration have not been promoted without native body recovery.
    public static class ProcessManager
    {
        private class SystemInfo
        {
            public readonly SystemRef SystemRef;
            public readonly Dictionary<string, ISystemRef> SystemRefDictionary;

            // 0x06000e50; arm64 0x1b18aa0.
            public SystemInfo(SystemRef systemRef)
            {
                SystemRefDictionary = new Dictionary<string, ISystemRef>();
                SystemRef = systemRef;
            }
        }

        // Relevant original static initializer field, token 0x06000e4e.
        // Other action-dispatch/logger fields are outside this recovered subset.
        private static readonly Dictionary<string, SystemInfo> s_systemDictionary = new Dictionary<string, SystemInfo>();

        // 0x06000e31/0x06000e32. Native virtual slot 3 is Type.ToString,
        // not Type.Name or Type.FullName; use the observed call itself.
        public static string GetDefaultName(ISystem system) => system.GetType().ToString();
        public static string GetDefaultName<T>() where T : class, ISystem => typeof(T).ToString();

        // 0x06000e33; arm64 generic 0x974da4.
        public static SystemRef RegisterSystem<T>(string systemName = null, bool canReplace = false, bool noException = false)
            where T : class, ISystem, new()
        {
            if (systemName == null) systemName = GetDefaultName<T>();
            return RegisterSystem(new T(), systemName, canReplace, noException);
        }

        // 0x06000e34; arm64 0x1b15b1c. Existing references are preserved;
        // replacement notifies the untyped reference before each typed cache.
        public static SystemRef RegisterSystem(ISystem system, string systemName = null, bool canReplace = false, bool noException = false)
        {
            if (systemName == null) systemName = GetDefaultName(system);
            if (s_systemDictionary.TryGetValue(systemName, out SystemInfo info))
            {
                if (ReferenceEquals(info.SystemRef.GetSafe(), system)) return info.SystemRef;
                if (!canReplace && !info.SystemRef.IsNull())
                {
                    if (noException) return info.SystemRef;
                    throw new InvalidOperationException("Trying to register over a system with the same name");
                }
                ((ISystemRef)info.SystemRef).InternalReplaceSystem(system);
                foreach (ISystemRef reference in info.SystemRefDictionary.Values) reference.InternalReplaceSystem(system);
                return info.SystemRef;
            }
            info = new SystemInfo(new SystemRef(systemName, system));
            s_systemDictionary[systemName] = info;
            return info.SystemRef;
        }

        // 0x06000e3c; arm64 0x1b19314. Missing rows are null without creation.
        public static bool IsSystemNull(string systemName) =>
            !s_systemDictionary.TryGetValue(systemName, out SystemInfo info) || info.SystemRef.IsNull();
        // 0x06000e3d; arm64 generic 0x974b3c.
        public static bool IsSystemNull<T>(string systemName = null) where T : class, ISystem => IsSystemNull(systemName ?? GetDefaultName<T>());

        // 0x06000e3e; arm64 0x1b1941c. Disabled auto-registration returns
        // a detached null reference; enabled registers a null-system row.
        public static SystemRef GetSystemRef(string systemName, bool autoRegister = true)
        {
            if (s_systemDictionary.TryGetValue(systemName, out SystemInfo info)) return info.SystemRef;
            return autoRegister ? RegisterSystem(null, systemName) : new SystemRef(systemName, null);
        }

        // 0x06000e3f; arm64 generic 0x974168. Cache typed references by
        // Type.ToString within the row. A newly created incompatible reference
        // starts null, even if its untyped system is non-null.
        public static SystemRef<T> GetSystemRef<T>(string systemName = null, bool autoRegister = true) where T : class, ISystem
        {
            if (systemName == null) systemName = GetDefaultName<T>();
            if (!s_systemDictionary.TryGetValue(systemName, out SystemInfo info))
            {
                if (!autoRegister) return new SystemRef<T>(systemName, null);
                RegisterSystem(null, systemName);
                info = s_systemDictionary[systemName];
            }
            string typeName = GetDefaultName<T>();
            if (info.SystemRefDictionary.TryGetValue(typeName, out ISystemRef existing))
                return existing as SystemRef<T>;
            var reference = new SystemRef<T>(info.SystemRef.SystemName(), info.SystemRef.GetSafe<T>());
            info.SystemRefDictionary[typeName] = reference;
            return reference;
        }

        // 0x06000e40; arm64 generic 0x973c10. Diagnostic/exception routing
        // through HLUnityCore remains an explicit unresolved null-system branch.
        public static T GetSystem<T>(string systemName = null, bool autoRegister = true) where T : class, ISystem
        {
            SystemRef reference = GetSystemRef(systemName ?? GetDefaultName<T>(), autoRegister);
            if (reference.IsNull())
                throw new NotSupportedException("Original ProcessManager null-system HLUnityCore diagnostic routing is not recovered.");
            return reference.Get<T>();
        }
    }
}
