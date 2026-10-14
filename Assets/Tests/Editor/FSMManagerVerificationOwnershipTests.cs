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
    // never fire or replace observers on rows it borrows. Each case restores the registry's
    // complete dictionary state so later fixtures see the same rows and free slots.
    public sealed class FSMManagerVerificationOwnershipTests
    {
        private static IDictionary Rows() => (IDictionary)typeof(ProcessManager)
            .GetField("s_systemDictionary", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

        [Test]
        public void RepeatedRunsLeaveRegistryRowsAndSystemsUnchanged()
        {
            IDictionary rows = Rows();
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
            string gap = "Lucid-ownership-gap-" + Guid.NewGuid().ToString("N");
            string probe = "Lucid-ownership-probe-" + Guid.NewGuid().ToString("N");
            try
            {
                // A slot freed before the run decides where the next registration lands.
                ProcessManager.GetSystemRef(gap);
                ProcessManager.GetSystemRef(Guid.NewGuid().ToString("N"));
                rows.Remove(gap);
                Dictionary<string, bool> before = rows.Keys.Cast<string>().ToDictionary(name => name, name => ProcessManager.IsSystemNull(name));
                List<string> order = rows.Keys.Cast<string>().ToList();
                object[] runLayout = FSMManagerVerification.CaptureLayout(rows);
                ProcessManager.GetSystemRef(probe);
                List<string> expected = rows.Keys.Cast<string>().ToList();
                FSMManagerVerification.RestoreLayout(rows, runLayout);
                FSMManagerVerification.Run();
                FSMManagerVerification.Run();
                Assert.That(rows.Keys.Cast<string>(), Is.EqualTo(order), "registry rows and their order");
                ProcessManager.GetSystemRef(probe);
                Assert.That(rows.Keys.Cast<string>(), Is.EqualTo(expected), "free slots that predate the run");
                foreach (KeyValuePair<string, bool> row in before)
                    Assert.That(ProcessManager.IsSystemNull(row.Key), Is.EqualTo(row.Value), row.Key);
            }
            finally
            {
                FSMManagerVerification.RestoreLayout(rows, layout);
            }
        }

        [Test]
        public void BorrowedEmptyDefaultManagerRowKeepsItsReferencesAndObservers()
        {
            IDictionary rows = Rows();
            string name = ProcessManager.GetDefaultName<FSMManager>();
            // An earlier fixture may have left a default manager row; set it aside for this case.
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
            rows.Remove(name);
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
                FSMManagerVerification.RestoreLayout(rows, layout);
            }
        }

        [Test]
        public void ThrowingShutdownObserverDoesNotKeepLaterOwnedRowsRegistered()
        {
            IDictionary rows = Rows();
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
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
                FSMManagerVerification.RestoreLayout(rows, layout);
            }
        }
    }
}
