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
    public sealed class FSMFixtureManagerRowOwnershipTests : ProjectLucid.Tests.OwnedGraphNamesFixture
    {
        public FSMFixtureManagerRowOwnershipTests()
            : base("Lucid-row-scope-", "Lucid-cleanup-shape-", "Lucid-borrowed-manager-") { }

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

        // Control for the cold-registry cases. GetManager (0x060003eb, x86-64 0x1ab8ba0) tests
        // IsSystemNull<FSMManager>(null) at 0x1ab8c11 and, when the row is null, constructs an
        // FSMManager (0x1ab8c2f) and registers it (RegisterSystem, 0x1ab8c52). A cleanup that
        // releases a fixture's machine through GetManager therefore leaves a live manager row on
        // a cold registry, after success and after an early failure alike; the same bodies run
        // inside the row scope leave none.
        [Test]
        public void CleanupThroughGetManagerLeavesALiveRowWhereTheRowScopeLeavesNone()
        {
            string name = "Lucid-cleanup-shape-" + Guid.NewGuid().ToString("N");
            // Success: the registering constructor reaches GetManager and AddFSM itself.
            WithColdDefaultRow(() =>
            {
                FiniteStateMachine machine = null;
                try { machine = new FiniteStateMachine(name); }
                finally { if (machine != null) FSMManager.GetManager().ReleaseFSM(machine); }
                Assert.That(ProcessManager.IsSystemValid<FSMManager>(), Is.True, "acquiring cleanup after success");
                Assert.That(ProcessManager.GetSystem<FSMManager>().FSMExists(name), Is.False);
            });
            WithColdDefaultRow(() =>
            {
                FSMManagerVerification.WithDefaultManagerRow(
                    () => new FiniteStateMachine(name),
                    manager => { while (manager.FSMExists(name)) manager.ReleaseFSM(name); });
                Assert.That(Rows().Contains(ManagerName), Is.False, "row scope after success");
            });

            // Early failure: nothing acquired the manager before the cleanup ran.
            var early = new InvalidOperationException("failure before any manager acquisition");
            WithColdDefaultRow(() =>
            {
                Exception caught = Assert.Throws<InvalidOperationException>(() =>
                {
                    try { throw early; }
                    finally { FSMManager.GetManager().ReleaseFSM(name); }
                });
                Assert.That(caught, Is.SameAs(early));
                Assert.That(ProcessManager.IsSystemValid<FSMManager>(), Is.True, "acquiring cleanup after early failure");
            });
            WithColdDefaultRow(() =>
            {
                Exception caught = Assert.Throws<InvalidOperationException>(() =>
                    FSMManagerVerification.WithDefaultManagerRow(() => throw early,
                        manager => { while (manager.FSMExists(name)) manager.ReleaseFSM(name); }));
                Assert.That(caught, Is.SameAs(early));
                Assert.That(Rows().Contains(ManagerName), Is.False, "row scope after early failure");
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
