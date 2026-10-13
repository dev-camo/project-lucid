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
