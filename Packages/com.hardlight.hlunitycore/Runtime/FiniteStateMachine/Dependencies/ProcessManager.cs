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

    // Original registry/actions/query behavior, including diagnostic callbacks
    // and the captured system reference used by checked getters.
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
        // order and the explicit default enum comparer.
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

        // 0x06000e40; ARM64 shared 0x973c10. Keep the captured reference
        // across diagnostics. If callbacks leave it null, its own Get<T>()
        // diagnoses a second time; callbacks can replace it between the checks.
        public static T GetSystem<T>(string systemName = null, bool autoRegister = true) where T : class, ISystem
        {
            SystemRef reference = GetSystemRef(systemName ?? GetDefaultName<T>(), autoRegister);
            if (reference.IsNull())
                HLUnityCore.LogOrThrowException("System '" + reference.SystemName() + "' is null");
            return reference.Get<T>();
        }

        // Original token 0x06000e2e; arm64 0x1b16984. One callback per
        // system/action, overwritten through the ordinary dictionary indexer.
        public static void SubscribeToAction(this ISystem system, SystemAction action, Action<object> callback) =>
            s_systemActionLookup.TryGetOrNew(action)[system] = callback;

        // Original tokens 0x06000e3a/0x06000e3b; arm64 0x1b19224/0x974c70.
        public static bool IsSystemValid(string systemName) =>
            s_systemDictionary.TryGetValue(systemName, out SystemInfo info) && info.SystemRef.IsValid();
        public static bool IsSystemValid<T>(string systemName = null) where T : class, ISystem => IsSystemValid(systemName ?? GetDefaultName<T>());

        // Original token 0x06000e41; shared arm64 0x9748c0. Retrieve the
        // cached typed reference, including its original incompatible-null rules.
        public static T GetSystemSafe<T>(string systemName = null, bool autoRegister = true) where T : class, ISystem =>
            GetSystemRef<T>(systemName ?? GetDefaultName<T>(), autoRegister).GetSafe();

        // Original token 0x06000e42; shared arm64 0x973dd4. The existing
        // system branch calls GetSystem<T>() with its default name, even when
        // this method received a custom name. Preserve that retail behavior.
        public static T GetSystemAutoCreate<T>(string systemName = null) where T : class, ISystem, new()
        {
            if (systemName == null) systemName = GetDefaultName<T>();
            if (IsSystemNull(systemName)) return RegisterSystem<T>(systemName).Get<T>();
            return GetSystem<T>();
        }

        // Original token 0x06000e43; arm64 0x1b195a4. Live enumeration
        // includes empty references; callback exceptions propagate.
        public static void ForEachSystem(Action<ISystem, SystemRef> callback)
        {
            foreach (SystemInfo info in s_systemDictionary.Values) callback(info.SystemRef.GetSafe(), info.SystemRef);
        }
        // Original token 0x06000e44; arm64 0x1b197bc. Safe means skip null
        // system values, not snapshot enumeration or swallowed callbacks.
        public static void ForEachSystemSafe(Action<ISystem, SystemRef> callback)
        {
            foreach (SystemInfo info in s_systemDictionary.Values)
            {
                ISystem system = info.SystemRef.GetSafe();
                if (system != null) callback(system, info.SystemRef);
            }
        }
        // Original token 0x06000e45; arm64 0x1b199e0.
        public static SystemRef FindSystemRef(Func<ISystem, SystemRef, bool> callback)
        {
            foreach (SystemInfo info in s_systemDictionary.Values)
                if (callback(info.SystemRef.GetSafe(), info.SystemRef)) return info.SystemRef;
            return null;
        }
        // Original token 0x06000e46; arm64 0x1b19c24.
        public static SystemRef FindSystemRefSafe(Func<ISystem, SystemRef, bool> callback)
        {
            foreach (SystemInfo info in s_systemDictionary.Values)
            {
                ISystem system = info.SystemRef.GetSafe();
                if (system != null && callback(system, info.SystemRef)) return info.SystemRef;
            }
            return null;
        }

        // Original token 0x06000e47, closure 0x06000e58; shared arm64
        // 0x974784/0xaf6bd8. Retain every matching registry alias.
        public static List<SystemRef> GetSystemRefsOfType<T>() where T : class, ISystem
        {
            var references = new List<SystemRef>();
            ForEachSystemSafe((system, reference) => { if (system is T) references.Add(reference); });
            return references;
        }
        // Original token 0x06000e48, closure 0x06000e5a; shared arm64
        // 0x974a10/0xaf6d14. Checked reference conversion is the observed call.
        public static List<T> GetSystemsOfType<T>() where T : class, ISystem
        {
            var systems = new List<T>();
            ForEachSystemSafe((system, reference) => { if (system is T) systems.Add(reference.Get<T>()); });
            return systems;
        }
        // Original token 0x06000e49, predicate 0x06000e53; shared arm64
        // 0x974544/0xaf14e8. Cast the actual untyped reference; do not create a
        // typed cache here. This cast normally returns null for T != ISystem.
        public static SystemRef<T> GetSystemRefOfType<T>() where T : class, ISystem =>
            FindSystemRefSafe((system, reference) => system is T) as SystemRef<T>;
        // Original token 0x06000e4a, predicate 0x06000e56; shared arm64
        // 0x973f5c/0xaf169c. No matching system returns null.
        public static T GetSystemOfType<T>() where T : class, ISystem =>
            FindSystemRefSafe((system, reference) => system is T)?.Get<T>();

        // Original token 0x06000e4b; arm64 0x1b19e74. Missing action rows
        // are created. A stored null callback throws when that system is found.
        public static void ProcessSystemAction(this ISystem system, SystemAction action, object context = null)
        {
            if (s_systemActionLookup.TryGetOrNew(action).TryGetValue(system, out Action<object> callback)) callback(context);
        }
        // Original token 0x06000e4c; arm64 0x1b19f98. No reentrancy guard or
        // finally resets the progress flag. Shared list snapshot isolates later
        // subscription edits; nested dispatch can invalidate its live enumerator.
        public static void ProcessSystemAction(SystemAction action, object context = null)
        {
            s_systemActionInProgress = true;
            Dictionary<ISystem, Action<object>> callbacks = s_systemActionLookup.TryGetOrNew(action);
            s_actionList.Clear();
            s_actionList.AddRange(callbacks.Values);
            foreach (Action<object> callback in s_actionList) callback(context);
            s_systemActionInProgress = false;
        }
        // Original token 0x06000e4d; arm64 0x1b1a208. A shutdown callback
        // exception aborts before clearing dictionaries/list and resetting flag.
        public static void ForceReset()
        {
            UnregisterAllSystems();
            s_systemDictionary.Clear();
            s_systemActionLookup.Clear();
            s_actionList.Clear();
            s_systemActionInProgress = false;
        }
    }
}
