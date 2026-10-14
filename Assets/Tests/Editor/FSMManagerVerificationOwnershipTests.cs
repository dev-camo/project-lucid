using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Editor
{
    // The FSM manager fixture must leave no fixture-owned registry row behind and must
    // never fire or replace observers on rows it borrows.
    public sealed class FSMManagerVerificationOwnershipTests
    {
        private static IDictionary Rows() => (IDictionary)typeof(ProcessManager)
            .GetField("s_systemDictionary", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

        [Test]
        public void RepeatedRunsLeaveRegistryRowsAndSystemsUnchanged()
        {
            IDictionary rows = Rows();
            Dictionary<string, bool> before = rows.Keys.Cast<string>().ToDictionary(name => name, name => ProcessManager.IsSystemNull(name));
            List<string> order = rows.Keys.Cast<string>().ToList();
            FSMManagerVerification.Run();
            FSMManagerVerification.Run();
            Assert.That(rows.Keys.Cast<string>(), Is.EqualTo(order), "registry rows and their order");
            foreach (KeyValuePair<string, bool> row in before)
                Assert.That(ProcessManager.IsSystemNull(row.Key), Is.EqualTo(row.Value), row.Key);
        }

        [Test]
        public void BorrowedEmptyDefaultManagerRowKeepsItsReferencesAndObservers()
        {
            IDictionary rows = Rows();
            string name = ProcessManager.GetDefaultName<FSMManager>();
            Assume.That(rows.Contains(name), Is.False, "this case owns the default manager row only when it is absent");
            // This test's own empty row stands in for one retained by another fixture.
            SystemRef<FSMManager> typed = ProcessManager.GetSystemRef<FSMManager>();
            SystemRef untyped = ProcessManager.GetSystemRef(name);
            object row = rows[name];
            // A later row makes the borrowed row's position observable.
            string later = "Lucid-ownership-order-" + Guid.NewGuid().ToString("N");
            ProcessManager.GetSystemRef(later);
            List<string> order = rows.Keys.Cast<string>().ToList();
            int calls = 0;
            untyped.OnSystemStartup += system => calls++;
            untyped.OnSystemShutdown += system => calls++;
            typed.OnSystemStartup += system => calls++;
            typed.OnSystemShutdown += system => calls++;
            try
            {
                FSMManagerVerification.Run();
                Assert.That(calls, Is.Zero, "borrowed observers ran");
                Assert.That(rows[name], Is.SameAs(row));
                Assert.That(ProcessManager.GetSystemRef<FSMManager>(), Is.SameAs(typed));
                Assert.That(ProcessManager.GetSystemRef(name), Is.SameAs(untyped));
                Assert.That(untyped.IsNull() && typed.IsNull(), Is.True);
                Assert.That(rows.Keys.Cast<string>(), Is.EqualTo(order), "registry order");
            }
            finally
            {
                rows.Remove(name);
                rows.Remove(later);
            }
        }

        [Test]
        public void ThrowingShutdownObserverDoesNotKeepLaterOwnedRowsRegistered()
        {
            IDictionary rows = Rows();
            string first = "Lucid-ownership-test-" + Guid.NewGuid().ToString("N");
            string second = first + "-second";
            try
            {
                SystemRef firstRef = ProcessManager.RegisterSystem(new FSMManager(), first);
                SystemRef<FSMManager> firstTyped = ProcessManager.GetSystemRef<FSMManager>(first);
                SystemRef secondRef = ProcessManager.RegisterSystem(new FSMManager(), second);
                var injected = new InvalidOperationException("injected shutdown failure");
                firstRef.OnSystemShutdown += system => throw injected;
                Exception thrown = Assert.Catch(() => FSMManagerVerification.ReleaseOwnedRows(new[] { first, second }));
                Assert.That(thrown, Is.SameAs(injected));
                Assert.That(rows.Contains(first) || rows.Contains(second), Is.False);
                Assert.That(secondRef.IsNull(), Is.True, "later owned row was not revoked");
                // The original stops before typed references; they are revoked individually.
                Assert.That(firstTyped.IsNull(), Is.True, "failing row's typed reference was not revoked");
                // Only the reference whose own observer throws keeps its system, as in the original revoke.
                Assert.That(firstRef.IsNull(), Is.False);
            }
            finally
            {
                rows.Remove(first);
                rows.Remove(second);
            }
        }
    }
}
