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

    // Original registry and unregistration subset. Engine action subscription,
    // dispatch and registry query/enumeration APIs remain unrecovered.
    public enum SystemAction { Configure, Initialise, Shutdown, AppInitialise, AppShutdown, Update }
    // Original metadata contract contains no members (type 0x02000057).
    public interface IConditionalLogger { }

    public static class ProcessManager
    {
        private class ProcessManagerLogger : IConditionalLogger
        {
            // Original token 0x06000e4f; arm64 0x1b1a4dc delegates Object.
            public ProcessManagerLogger() { }
        }
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

        private static readonly Dictionary<string, SystemInfo> s_systemDictionary;
        private static readonly Dictionary<SystemAction, Dictionary<ISystem, Action<object>>> s_systemActionLookup;
        private static readonly List<Action<object>> s_actionList;
        private static bool s_systemActionInProgress;
        private static readonly ProcessManagerLogger s_logger;

        // Original token 0x06000e4e; arm64 0x1b1a328. Preserve initialization
        // order and the explicit default enum comparer; dispatch is unrecovered.
        static ProcessManager()
        {
            s_systemDictionary = new Dictionary<string, SystemInfo>();
            s_systemActionLookup = new Dictionary<SystemAction, Dictionary<ISystem, Action<object>>>(EqualityComparer<SystemAction>.Default);
            s_actionList = new List<Action<object>>();
            s_logger = new ProcessManagerLogger();
        }

        // Original token 0x06000e2f; arm64 0x1b187c4. Empty action rows stay.
        public static bool UnsubscribeFromAction(this ISystem system, SystemAction action) =>
            s_systemActionLookup.TryGetValue(action, out Dictionary<ISystem, Action<object>> callbacks) && callbacks.Remove(system);

        // Original token 0x06000e30; arm64 0x1b188b4. No callbacks, row pruning,
        // action-list changes or dispatch-state guard occur during removal.
        public static void UnsubscribeFromAllActions(ISystem system)
        {
            foreach (KeyValuePair<SystemAction, Dictionary<ISystem, Action<object>>> entry in s_systemActionLookup)
                entry.Value.Remove(system);
        }

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

        // Original token 0x06000e35; shared arm64 0x974ee4.
        public static void UnregisterSystem<T>() where T : class, ISystem => UnregisterSystem(GetDefaultName<T>());

        // Original token 0x06000e36; arm64 0x1b18b48. Rows and cached
        // references survive unregistration, so later registration reuses them.
        public static void UnregisterSystem(string systemName)
        {
            if (s_systemDictionary.TryGetValue(systemName, out SystemInfo info)) UnregisterSystemInternal(info);
        }

        // Original token 0x06000e37; arm64 0x1b161f4. Enumerate live values
        // and process every identity match, including null matches. Reentrant
        // additions can invalidate this ordinary managed enumerator.
        public static void UnregisterSystem(ISystem system)
        {
            foreach (SystemInfo info in s_systemDictionary.Values)
                if (ReferenceEquals(info.SystemRef.GetSafe(), system)) UnregisterSystemInternal(info);
        }

        // Original token 0x06000e38; arm64 0x1b18eb4. Snapshot rows before
        // callbacks; newly registered rows are outside this teardown operation.
        public static void UnregisterAllSystems()
        {
            var snapshot = new List<SystemInfo>(s_systemDictionary.Values);
            foreach (SystemInfo info in snapshot) UnregisterSystemInternal(info);
        }

        // Original token 0x06000e39; arm64 0x1b18c24. Unsubscribe first,
        // revoke untyped then typed. Exceptions retain preceding mutations and
        // stop later revokes; ordinary foreach disposal does not swallow them.
        private static void UnregisterSystemInternal(SystemInfo existingSystemInfo)
        {
            ISystem system = existingSystemInfo.SystemRef.GetSafe();
            if (system != null) UnsubscribeFromAllActions(system);
            ((ISystemRef)existingSystemInfo.SystemRef).InternalRevokeSystem();
            foreach (ISystemRef reference in existingSystemInfo.SystemRefDictionary.Values) reference.InternalRevokeSystem();
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
