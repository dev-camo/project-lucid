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
        // run nor consumed. An absent row becomes the fixture's own and is
        // removed once cleared; an existing row must be idle on its untyped and
        // every typed reference, and stays. With borrowLiveSystem, a row whose
        // system is already registered is borrowed as it is, because the
        // fixture then never registers into it.
        public sealed class LifecycleRow : IDisposable
        {
            private static readonly string[] OwnershipFields = { "m_system", "m_isystem", "OnSystemStartup", "OnSystemShutdown", "m_actionOnSystemValid" };
            private readonly IDictionary registry;
            private readonly string name;
            private readonly object borrowedRow;
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
            }

            private static SystemRef Reference(object row) => (SystemRef)Read(row, "SystemRef");

            private static object Read(object value, string field) =>
                value.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(value);

            private static void RequireIdle(object row, string name)
            {
                RequireIdleReference(Reference(row), name);
                foreach (DictionaryEntry entry in (IDictionary)Read(row, "SystemRefDictionary")) RequireIdleReference(entry.Value, name);
            }

            private static void RequireIdleReference(object reference, string name)
            {
                Type type = reference.GetType();
                while (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(SystemRef<>)) type = type.BaseType;
                foreach (string field in OwnershipFields)
                    if (type.GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).GetValue(reference) != null)
                        throw new InvalidOperationException("Fixture registry row retains unowned state: " + name + "/" + field);
            }

            // Call after the fixture has unregistered its systems. A row that
            // still holds state is left in place and reported.
            public void Dispose()
            {
                if (released) return;
                released = true;
                if (BorrowsLiveSystem) return;
                if (Existed)
                {
                    if (!ReferenceEquals(registry[name], borrowedRow))
                        throw new InvalidOperationException("Fixture borrowed registry row was replaced: " + name);
                    RequireIdle(borrowedRow, name);
                    return;
                }
                if (!registry.Contains(name)) return;
                RequireIdle(registry[name], name);
                registry.Remove(name);
            }
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
                registration();
                object row = registry[name];
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
