using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;

namespace ProjectLucid
{
    // Maintainer fixtures must not replace retained registry references or
    // execute their observers while borrowing an apparently empty row.
    public static class VerificationFixtureOwnership
    {
        public static void RequireAbsent(IDictionary registry, string name, params object[] sharedState)
        {
            if (registry.Contains(name))
                throw new InvalidOperationException("Fixture requires an absent registry row: " + name);
            foreach (object value in sharedState)
                if (!ReferenceEquals(value, null))
                    throw new InvalidOperationException("Fixture requires unowned static state: " + name);
        }

        // Admits a row that the original lifecycle registers into, reading
        // fields only so a refused row's observers and deferred work are neither
        // run nor consumed. Same admission and release model as the PlayMode
        // LifecycleSystemRowLease, which this Editor assembly cannot reference.
        // An absent row becomes the proof's own and is removed once cleared; an
        // existing row must be idle on its untyped and every typed reference,
        // and stays, losing only the cleared typed entries added while
        // borrowed. With borrowLiveSystem, a row whose system is already
        // registered is borrowed as it is, because the proof then never
        // registers into it.
        //
        // The proofs never hand their references to the row, so a created row
        // or typed entry is not captured as the PlayMode lease does. Each proof
        // is synchronous and holds the key from admission to release, so a row
        // or typed entry first found at release was created while it ran; it is
        // removed only when its untyped and every typed reference are cleared.
        public sealed class LifecycleRow : IDisposable
        {
            private static readonly string[] OwnershipFields = { "m_system", "m_isystem", "OnSystemStartup", "OnSystemShutdown", "m_actionOnSystemValid" };
            private readonly IDictionary registry;
            private readonly string name;
            private readonly object borrowedRow;
            private readonly object borrowedReference;
            private readonly IDictionary borrowedCache;
            private readonly Dictionary<object, object> priorTypedEntries;
            private bool released;

            public bool Existed { get; }
            public bool BorrowsLiveSystem { get; }

            public LifecycleRow(string name, bool borrowLiveSystem = false)
                : this((IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null), name, borrowLiveSystem) { }

            public LifecycleRow(IDictionary registry, string name, bool borrowLiveSystem = false)
            {
                this.registry = registry;
                this.name = name;
                if (!registry.Contains(name)) return;
                object row = registry[name];
                if (row == null) throw new InvalidOperationException("Fixture requires an absent or idle registry row: " + name);
                if (borrowLiveSystem && Reference(row).IsValid()) BorrowsLiveSystem = true;
                else RequireIdle(row, name);
                Existed = true;
                borrowedRow = row;
                borrowedReference = Reference(row);
                borrowedCache = TypedCache(row);
                priorTypedEntries = new Dictionary<object, object>();
                foreach (DictionaryEntry entry in borrowedCache) priorTypedEntries.Add(entry.Key, entry.Value);
            }

            private static SystemRef Reference(object row) => (SystemRef)Read(row, "SystemRef");

            private static IDictionary TypedCache(object row) => (IDictionary)Read(row, "SystemRefDictionary");

            private static object Read(object value, string field) =>
                value.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(value);

            private static void RequireIdle(object row, string name)
            {
                RequireIdleReference(Reference(row), name);
                foreach (DictionaryEntry entry in TypedCache(row)) RequireIdleReference(entry.Value, name);
            }

            private static void RequireIdleReference(object reference, string name)
            {
                Type type = reference.GetType();
                while (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(SystemRef<>)) type = type.BaseType;
                foreach (string field in OwnershipFields)
                    if (type.GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).GetValue(reference) != null)
                        throw new InvalidOperationException("Fixture registry row retains unowned state: " + name + "/" + field);
            }

            // Call after the proof has unregistered its systems. A row that
            // still holds state is left in place and reported.
            public void Dispose()
            {
                if (released) return;
                released = true;
                if (BorrowsLiveSystem) return;
                if (Existed)
                {
                    ReturnBorrowedRow();
                    return;
                }
                if (!registry.Contains(name)) return;
                RequireIdle(registry[name], name);
                registry.Remove(name);
            }

            // The borrowed row and its prior typed entries stay as admitted;
            // typed entries added while borrowed are removed once cleared.
            private void ReturnBorrowedRow()
            {
                if (!ReferenceEquals(registry[name], borrowedRow) ||
                    !ReferenceEquals(Reference(borrowedRow), borrowedReference) ||
                    !ReferenceEquals(TypedCache(borrowedRow), borrowedCache))
                    throw new InvalidOperationException("Fixture borrowed registry row was replaced: " + name);
                foreach (KeyValuePair<object, object> entry in priorTypedEntries)
                    if (!borrowedCache.Contains(entry.Key) || !ReferenceEquals(borrowedCache[entry.Key], entry.Value))
                        throw new InvalidOperationException("Fixture borrowed typed cache changed: " + name);
                RequireIdle(borrowedRow, name);
                var added = new List<object>();
                foreach (DictionaryEntry entry in borrowedCache)
                    if (!priorTypedEntries.ContainsKey(entry.Key)) added.Add(entry.Key);
                foreach (object key in added) borrowedCache.Remove(key);
            }
        }

        // Removes a row the fixture's own construction created, only while every reference in
        // it is still empty; a row that gained a system or observer is foreign state and stays.
        public static void ReleaseEmptyRow(IDictionary registry, string name, object row)
        {
            var untyped = (SystemRef)row.GetType().GetField("SystemRef").GetValue(row);
            var cache = (IDictionary)row.GetType().GetField("SystemRefDictionary").GetValue(row);
            if (!untyped.IsNull()) throw new InvalidOperationException("Fixture row gained a system: " + name);
            foreach (DictionaryEntry entry in cache)
            {
                Type type = entry.Value.GetType();
                while (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(SystemRef<>)) type = type.BaseType;
                foreach (string field in new[] { "m_system", "m_isystem", "OnSystemStartup", "OnSystemShutdown", "m_actionOnSystemValid" })
                    if (type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly).GetValue(entry.Value) != null)
                        throw new InvalidOperationException("Fixture row reference retains state: " + name + "." + field);
            }
            registry.Remove(name);
        }

        public sealed class RegistryRow : IDisposable
        {
            private readonly IDictionary registry;
            private readonly string name;
            private readonly List<ISystem> systems = new List<ISystem>();
            private object ownedRow;

            public RegistryRow(string name, params object[] sharedState)
            {
                registry = (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                RequireAbsent(registry, name, sharedState);
                this.name = name;
            }

            private SystemRef Reference(object row) => (SystemRef)row.GetType().GetField("SystemRef").GetValue(row);

            private void RequireOwnedRow()
            {
                if (!ReferenceEquals(registry[name], ownedRow))
                    throw new InvalidOperationException("Fixture registry row was replaced: " + name);
                ISystem current = Reference(ownedRow).GetSafe();
                if (current != null && !systems.Any(system => ReferenceEquals(system, current)))
                    throw new InvalidOperationException("Fixture registry row has a foreign owner: " + name);
            }

            public SystemRef Register(ISystem system, bool canReplace = false)
            {
                if (ownedRow == null) RequireAbsent(registry, name);
                else RequireOwnedRow();
                // A replacement callback can throw after publishing this owned
                // system. Keep that partial acquisition eligible for cleanup.
                systems.Add(system);
                SystemRef reference = ProcessManager.RegisterSystem(system, name, canReplace);
                if (ownedRow == null) ownedRow = registry[name];
                return reference;
            }

            public void RegisterUsing(ISystem system, Action registration)
            {
                if (ownedRow != null) throw new InvalidOperationException("Fixture already acquired this row: " + name);
                RequireAbsent(registry, name);
                object row;
                try
                {
                    registration();
                }
                catch
                {
                    // A lifecycle callback can throw after publishing this owned
                    // system in the newly admitted row; keep it eligible for cleanup.
                    row = registry[name];
                    if (row != null && ReferenceEquals(Reference(row).GetSafe(), system))
                    {
                        systems.Add(system);
                        ownedRow = row;
                    }
                    throw;
                }
                row = registry[name];
                if (row == null || !ReferenceEquals(Reference(row).GetSafe(), system))
                    throw new InvalidOperationException("Fixture registration did not acquire its expected system: " + name);
                systems.Add(system);
                ownedRow = row;
            }

            public void Dispose()
            {
                if (ownedRow == null) return;
                RequireOwnedRow();
                ProcessManager.UnregisterSystem(name);
                RequireOwnedRow();
                if (!Reference(ownedRow).IsNull())
                    throw new InvalidOperationException("Fixture unregister retained a system: " + name);
                registry.Remove(name);
                ownedRow = null;
            }
        }
    }
}
