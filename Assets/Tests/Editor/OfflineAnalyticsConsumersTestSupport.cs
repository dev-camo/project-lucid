using System.Collections;
using System.Reflection;
using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // Shared by the cases of the four consumers that route through ProjectLucid.Offline.OfflineAnalyticsConsumers
    // (issue 414). Earlier code may legitimately leave an idle registry row for a system (a static SystemRef created by a
    // type initialiser), so the cases do not assume a row is absent: a case leases the row (Owned.LeaseRow, which restores
    // exactly what it found), asserts that no manager is registered, and asserts afterwards that the consumers neither
    // registered one nor created or removed a row.
    internal static class OfflineAnalyticsConsumersTestSupport
    {
        // True when the registry has a key for the system's default name, idle or not.
        internal static bool RowPresent<T>() where T : class, ISystem
        {
            var registry = (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            return registry.Contains(ProcessManager.GetDefaultName<T>());
        }

        // True when nothing is registered as the system; a non-registering lookup, so it leaves no row behind.
        internal static bool NoManagerRegistered<T>() where T : class, ISystem =>
            ProcessManager.GetSystemSafe<T>(null, false) == null;

        // The precondition a case owns: no manager is registered. Returns whether an (idle) row exists, to compare afterwards.
        internal static bool RequireNoManager<T>() where T : class, ISystem
        {
            Assert.That(NoManagerRegistered<T>(), Is.True, typeof(T).Name + " must not be registered");
            return RowPresent<T>();
        }

        // After the consumers ran: still no manager, and the row exists exactly as before (none created, none removed).
        internal static void AssertUntouched<T>(bool rowBefore, string message) where T : class, ISystem
        {
            Assert.That(NoManagerRegistered<T>(), Is.True, message + " (no manager registered)");
            Assert.That(RowPresent<T>(), Is.EqualTo(rowBefore), message + " (row presence unchanged)");
        }
    }
}
