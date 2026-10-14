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

        private sealed class ForeignSystem : ISystem { }

        [Test]
        public void FailureDuringRunStillReleasesOwnedRowsAndRestoresTheRegistry()
        {
            IDictionary rows = Rows();
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
            try
            {
                // A registered HLUnityCore breaks the diagnostics precondition, after the
                // fixture has already acquired its own rows and references.
                SystemRef core = ProcessManager.RegisterSystem(new ForeignSystem(), "Hardlight.HLUnityCore", noException: true);
                List<string> order = rows.Keys.Cast<string>().ToList();
                List<object> values = rows.Values.Cast<object>().ToList();
                var failure = Assert.Throws<InvalidOperationException>(FSMManagerVerification.Run);
                Assert.That(failure.Message, Is.EqualTo("FSM manager verification failed: diagnostic test precondition"));
                Assert.That(rows.Keys.Cast<string>(), Is.EqualTo(order), "no owned row survives and order is kept");
                Assert.That(rows.Values.Cast<object>(), Is.EqualTo(values), "row objects are unchanged");
                Assert.That(core.IsValid(), Is.True, "the foreign row keeps its system");
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
                int untypedCalls = 0, typedCalls = 0;
                // Persistent observers throw on every call, on both the untyped and a typed reference.
                firstRef.OnSystemShutdown += system => { untypedCalls++; throw injected; };
                firstTyped.OnSystemShutdown += system => { typedCalls++; throw new InvalidOperationException("typed"); };
                Exception thrown = Assert.Catch(() => FSMManagerVerification.ReleaseOwnedRows(new[] { first, second }));
                Assert.That(thrown, Is.SameAs(injected));
                Assert.That(rows.Contains(first) || rows.Contains(second), Is.False);
                Assert.That(secondRef.IsNull(), Is.True, "later owned row was not revoked");
                // No owned reference survives, and each observer is notified exactly once.
                Assert.That(firstRef.IsNull() && firstTyped.IsNull(), Is.True, "failing row's references were not revoked");
                Assert.That(untypedCalls, Is.EqualTo(1), "throwing untyped observer was retried");
                Assert.That(typedCalls, Is.EqualTo(1), "typed observer was not notified exactly once");
            }
            finally
            {
                FSMManagerVerification.RestoreLayout(rows, layout);
            }
        }
    }
}
