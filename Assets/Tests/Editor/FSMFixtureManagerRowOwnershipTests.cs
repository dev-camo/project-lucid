using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Editor
{
    // FSMManager.GetManager (0x060003eb) registers a new default manager whenever the default
    // row is null. The FSM fixtures that reach it must own that row only when it was absent,
    // borrow a live manager in place and set a borrowed empty row aside without firing it.
    public sealed class FSMFixtureManagerRowOwnershipTests
    {
        private static readonly string ManagerName = ProcessManager.GetDefaultName<FSMManager>();

        private static readonly KeyValuePair<string, Action>[] Fixtures =
        {
            new KeyValuePair<string, Action>(nameof(FSMConstructionVerification), FSMConstructionVerification.Run),
            new KeyValuePair<string, Action>(nameof(FSMFileLoadingVerification), FSMFileLoadingVerification.Run),
            new KeyValuePair<string, Action>(nameof(FSMScriptableLoadingVerification), FSMScriptableLoadingVerification.Run),
            new KeyValuePair<string, Action>(nameof(FSMCompositionVerification), FSMCompositionVerification.Run),
        };

        private static IDictionary Rows() => (IDictionary)typeof(ProcessManager)
            .GetField("s_systemDictionary", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

        // Runs body with no default manager row, then releases whatever row it left and puts
        // the registry's complete dictionary state back.
        private static void WithColdDefaultRow(Action body)
        {
            IDictionary rows = Rows();
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
            try
            {
                rows.Remove(ManagerName);
                body();
            }
            finally
            {
                if (rows.Contains(ManagerName)) FSMManagerVerification.ReleaseOwnedRows(new[] { ManagerName });
                FSMManagerVerification.RestoreLayout(rows, layout);
            }
        }

        [Test]
        public void ColdRegistryRunsLeaveNoDefaultManagerRow()
        {
            foreach (KeyValuePair<string, Action> fixture in Fixtures)
                WithColdDefaultRow(() =>
                {
                    fixture.Value();
                    Assert.That(Rows().Contains(ManagerName), Is.False, fixture.Key);
                });
        }

        [Test]
        public void EarlyFailureInsideTheRowScopeLeavesNoDefaultManagerRow()
        {
            WithColdDefaultRow(() =>
            {
                var early = new InvalidOperationException("failure before any manager acquisition");
                int releases = 0;
                Exception caught = Assert.Throws<InvalidOperationException>(() =>
                    FSMManagerVerification.WithDefaultManagerRow(() => throw early, manager => ++releases));
                Assert.That(caught, Is.SameAs(early));
                Assert.That(releases, Is.Zero, "no manager is looked up when none was registered");
                Assert.That(Rows().Contains(ManagerName), Is.False, "cleanup created a manager row");

                var late = new InvalidOperationException("failure after a machine registered");
                FSMManager acquired = null, released = null;
                caught = Assert.Throws<InvalidOperationException>(() => FSMManagerVerification.WithDefaultManagerRow(() =>
                {
                    acquired = FSMManager.GetManager();
                    acquired.AddFSM(new FiniteStateMachine("Lucid-row-scope-" + Guid.NewGuid().ToString("N"), skipAddToManager: true));
                    throw late;
                }, manager => released = manager));
                Assert.That(caught, Is.SameAs(late));
                Assert.That(released, Is.SameAs(acquired), "the fixture's machines are released from the registered manager");
                Assert.That(Rows().Contains(ManagerName), Is.False, "the owned manager row survived");
            });
        }

        // Negative control: the cleanup shape the fixtures used before acquires the manager in
        // its finally block, which leaves a newly registered live manager behind.
        [Test]
        public void CleanupThatAcquiresTheManagerLeavesALiveRow()
        {
            WithColdDefaultRow(() =>
            {
                FSMManagerVerification.WithDefaultManagerRow(() => { }, manager => { });
                Assert.That(Rows().Contains(ManagerName), Is.False, "the row scope never registers a manager");
                try { }
                finally { FSMManager.GetManager(); }
                Assert.That(Rows().Contains(ManagerName), Is.True);
                Assert.That(ProcessManager.IsSystemValid<FSMManager>(), Is.True);
            });
        }

        [Test]
        public void BorrowedLiveManagerKeepsItsInstanceAndHoldsNoFixtureMachines()
        {
            foreach (KeyValuePair<string, Action> fixture in Fixtures)
                WithColdDefaultRow(() =>
                {
                    var live = new FSMManager();
                    ProcessManager.RegisterSystem(live, ManagerName);
                    var foreign = new FiniteStateMachine("Lucid-borrowed-manager-" + Guid.NewGuid().ToString("N"), skipAddToManager: true);
                    live.AddFSM(foreign);
                    object row = Rows()[ManagerName];
                    fixture.Value();
                    Assert.That(Rows()[ManagerName], Is.SameAs(row), fixture.Key);
                    Assert.That(ProcessManager.GetSystem<FSMManager>(), Is.SameAs(live), fixture.Key);
                    Assert.That(live.FSMs.Keys, Is.EquivalentTo(new[] { foreign.FSMId }), fixture.Key + " left machines in the borrowed manager");
                    Assert.That(live.TryGetFSM(foreign.FSMId, out FiniteStateMachine kept) && ReferenceEquals(kept, foreign), Is.True, fixture.Key);
                });
        }

        [Test]
        public void BorrowedEmptyRowKeepsItsObjectReferencesAndObservers()
        {
            foreach (KeyValuePair<string, Action> fixture in Fixtures)
                WithColdDefaultRow(() =>
                {
                    IDictionary rows = Rows();
                    // This test's own empty row stands in for one retained by another fixture.
                    SystemRef<FSMManager> typed = ProcessManager.GetSystemRef<FSMManager>();
                    SystemRef untyped = ProcessManager.GetSystemRef(ManagerName);
                    object row = rows[ManagerName];
                    // A later row makes the borrowed row's position observable.
                    string later = "Lucid-fixture-row-order-" + Guid.NewGuid().ToString("N");
                    ProcessManager.GetSystemRef(later);
                    List<string> order = rows.Keys.Cast<string>().ToList();
                    int calls = 0;
                    untyped.OnSystemStartup += system => calls++;
                    untyped.OnSystemShutdown += system => calls++;
                    typed.OnSystemStartup += system => calls++;
                    typed.OnSystemShutdown += system => calls++;
                    fixture.Value();
                    Assert.That(calls, Is.Zero, fixture.Key + " fired borrowed observers");
                    Assert.That(rows[ManagerName], Is.SameAs(row), fixture.Key);
                    Assert.That(ProcessManager.GetSystemRef<FSMManager>(), Is.SameAs(typed), fixture.Key);
                    Assert.That(ProcessManager.GetSystemRef(ManagerName), Is.SameAs(untyped), fixture.Key);
                    Assert.That(untyped.IsNull() && typed.IsNull(), Is.True, fixture.Key);
                    Assert.That(rows.Keys.Cast<string>(), Is.EqualTo(order), fixture.Key + " registry order");
                });
        }
    }
}
