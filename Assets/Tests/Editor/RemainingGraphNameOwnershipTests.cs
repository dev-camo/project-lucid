using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    public sealed class RemainingGraphNameOwnershipTests
    {
        private const string OfflinePrefix = "Lucid-offline-boot-";
        private const string ManagerPrefix = "Lucid-verification-manager-";
        private const string DiagnosticKey = "Lucid-verification-invalid-read";
        private const string BootPrefix = "ProjectLucid-Boot-state-proof-";
        private static readonly string[] RowPrefixes =
            { "Lucid-row-scope-", "Lucid-cleanup-shape-", "Lucid-borrowed-manager-" };
        private static Dictionary<int, string> Names => (Dictionary<int, string>)typeof(GraphNameLookup)
            .GetField("s_nameLookup", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

        [TestCase("FairPlay", false)]
        [TestCase("FairPlay", true)]
        [TestCase("Splash", false)]
        [TestCase("Splash", true)]
        [TestCase("Arm", false)]
        [TestCase("Arm", true)]
        [TestCase("RowFailure", false)]
        [TestCase("RowFailure", true)]
        [TestCase("RowCleanup", false)]
        [TestCase("RowCleanup", true)]
        [TestCase("RowBorrowed", false)]
        [TestCase("RowBorrowed", true)]
        public void FixtureLifecycleReleasesNamesAfterEachInvocation(string route, bool failAfterBody)
        {
            bool offline = route == "FairPlay" || route == "Splash" || route == "Arm";
            WithAudit(offline ? new[] { OfflinePrefix } : RowPrefixes, audit =>
            {
                for (int run = 0; run < 2; run++)
                {
                    object fixture;
                    Action body;
                    if (offline)
                    {
                        var value = new OfflineBootHookTests();
                        fixture = value;
                        if (route == "FairPlay") body = value.FairPlayCheckEntersThroughItsSuccessPathWithoutTheNativeLibrary;
                        else if (route == "Splash") body = value.TheSplashListenerCallWaitsUntilTheGraphHasLeftBoot;
                        else body = value.ArmWaitsForTheNamedSceneAndLeavesTheEventThere;
                    }
                    else
                    {
                        var value = new ProjectLucid.Editor.FSMFixtureManagerRowOwnershipTests();
                        fixture = value;
                        if (route == "RowFailure") body = value.EarlyFailureInsideTheRowScopeLeavesNoDefaultManagerRow;
                        else if (route == "RowCleanup") body = value.CleanupThroughGetManagerLeavesALiveRowWhereTheRowScopeLeavesNone;
                        else body = value.BorrowedLiveManagerKeepsItsInstanceAndHoldsNoFixtureMachines;
                    }
                    var failure = new InvalidProgramException("fixture body completed with registered names");
                    Action invoke = () => InvokeLifecycle(fixture, () =>
                    {
                        body();
                        audit.AssertAcquired();
                        audit.ObserveUnownedNames();
                        if (failAfterBody) throw failure;
                    });
                    if (failAfterBody) Assert.AreSame(failure, Assert.Throws<InvalidProgramException>(() => invoke()));
                    else invoke();
                    audit.AssertReleased();
                }
            });
        }

        [TestCase("Manager", false)]
        [TestCase("Manager", true)]
        [TestCase("Boot", false)]
        [TestCase("Boot", true)]
        [TestCase("BootManaged", false)]
        [TestCase("BootManaged", true)]
        public void PublicHelperReleasesNamesAfterEachInvocation(string route, bool failAfterRegistration)
        {
            bool manager = route == "Manager";
            WithAudit(manager ? new[] { ManagerPrefix, DiagnosticKey } : new[] { BootPrefix }, audit =>
            {
                TestContext.WriteLine("Fixed diagnostic key present at entry: " + Names.Values.Contains(DiagnosticKey));
                Action invoke = manager ? (Action)FSMManagerVerification.Run : route == "Boot"
                    ? BootPersistenceStateVerification.Run : () => { BootPersistenceStateVerification.RunManaged(); };
                string site = manager ? FSMManagerVerification.DiagnosticNamesRegisteredSite
                    : BootPersistenceStateVerification.NamesRegisteredSite;
                for (int run = 0; run < 2; run++)
                {
                    var failure = new InvalidProgramException("helper names registered");
                    bool reached = false;
                    Action<string> borrowed = VerificationFixtureOwnership.FaultInjection;
                    try
                    {
                        VerificationFixtureOwnership.FaultInjection = point =>
                        {
                            if (point != site) return;
                            reached = true;
                            audit.AssertAcquired();
                            if (manager)
                            {
                                Assert.That(Names.Values.Contains(DiagnosticKey), Is.True);
                                Assert.That(audit.NewOwnedNames.Any(name => name.EndsWith("-global", StringComparison.Ordinal)), Is.True);
                            }
                            audit.ObserveUnownedNames();
                            if (failAfterRegistration) throw failure;
                        };
                        if (failAfterRegistration) Assert.AreSame(failure, Assert.Throws<InvalidProgramException>(() => invoke()));
                        else invoke();
                        Assert.That(reached, Is.True, "the public helper reached actual name registration");
                        // Check before our recovery cleanup, so the test cannot conceal a missing helper scope.
                        audit.AssertReleased();
                    }
                    finally { VerificationFixtureOwnership.FaultInjection = borrowed; }
                }
            });
        }

        [Test]
        public void ManagerDiagnosticsPreconditionFailureReleasesNames()
        {
            WithAudit(new[] { ManagerPrefix, DiagnosticKey }, audit =>
            {
                using (var core = new OwnedCoreErrorLog())
                {
                    for (int run = 0; run < 2; run++)
                    {
                        bool acquired = false;
                        Action<string> borrowed = VerificationFixtureOwnership.FaultInjection;
                        try
                        {
                            VerificationFixtureOwnership.FaultInjection = site =>
                            {
                                if (site != FSMManagerVerification.ManagerNamesRegisteredSite) return;
                                audit.AssertAcquired();
                                audit.ObserveUnownedNames();
                                acquired = true;
                            };
                            var failure = Assert.Throws<InvalidOperationException>(FSMManagerVerification.Run);
                            Assert.That(failure.Message, Is.EqualTo("FSM manager verification failed: diagnostic test precondition"));
                            Assert.That(acquired, Is.True);
                            Assert.That(core.Errors, Is.Empty);
                            audit.AssertReleased();
                        }
                        finally { VerificationFixtureOwnership.FaultInjection = borrowed; }
                    }
                }
            });
        }

        [Test]
        public void ManagerProofPreservesThePreexistingFixedDiagnosticKey()
        {
            // If borrowed already, leave the exact row untouched. Otherwise this test owns its seed.
            bool existed = Names.Values.Contains(DiagnosticKey);
            int key = GraphNameLookup.ConvertNameToId(DiagnosticKey);
            try
            {
                WithAudit(new[] { ManagerPrefix, DiagnosticKey }, audit =>
                {
                    for (int run = 0; run < 2; run++)
                    {
                        FSMManagerVerification.Run();
                        Assert.That(Names[key], Is.EqualTo(DiagnosticKey));
                        audit.AssertReleased();
                    }
                });
            }
            finally { if (!existed) Names.Remove(key); }
        }

        private static void InvokeLifecycle(object fixture, Action body)
        {
            // Calling a fixture's test method directly skips NUnit's [SetUp] and [TearDown], so the
            // graph-name scope they would open and release is dispatched here.
            var names = fixture as OwnedGraphNamesFixture;
            names?.OpenOwnedGraphNames();
            try { body(); }
            finally { names?.ReleaseOwnedGraphNames(); }
        }

        private static void WithAudit(string[] prefixes, Action<NameAudit> body)
        {
            var audit = new NameAudit(prefixes);
            VerificationFixtureOwnership.RunThenCleanUp(() => body(audit), audit.Dispose);
        }

        private sealed class NameAudit : IDisposable
        {
            private readonly Dictionary<int, string> initial = new Dictionary<int, string>(Names);
            private readonly Dictionary<int, string> preserved;
            private readonly string[] prefixes;
            private readonly string unrelated = "Lucid-graph-ownership-foreign-" + Guid.NewGuid().ToString("N");

            public NameAudit(string[] prefixes)
            {
                this.prefixes = prefixes;
                try
                {
                    foreach (string prefix in prefixes)
                        GraphNameLookup.ConvertNameToId(prefix + "preexisting-" + Guid.NewGuid().ToString("N"));
                    GraphNameLookup.ConvertNameToId(unrelated);
                    preserved = new Dictionary<int, string>(Names);
                }
                catch { Dispose(); throw; }
            }

            private bool Owned(string name) => name != null && prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));
            public IEnumerable<string> NewOwnedNames => Names.Where(row => !preserved.ContainsKey(row.Key) && Owned(row.Value)).Select(row => row.Value);
            public void AssertAcquired() => Assert.That(NewOwnedNames, Is.Not.Empty, "the actual body must register names before cleanup/failure");

            public void ObserveUnownedNames()
            {
                // Preserve legitimate lazy production registrations without assuming whole-table equality.
                foreach (var row in Names)
                    if (!Owned(row.Value) && !preserved.ContainsKey(row.Key)) preserved.Add(row.Key, row.Value);
            }

            public void AssertReleased()
            {
                foreach (var row in preserved)
                {
                    Assert.That(Names.TryGetValue(row.Key, out string value), Is.True, row.Value);
                    Assert.That(value, Is.EqualTo(row.Value));
                }
                Assert.That(NewOwnedNames, Is.Empty, "new fixture-owned names survived the boundary");
            }

            public void Dispose()
            {
                // Recovery runs after assertions, including an intentional tests-first failure. Never
                // restore the table or remove borrowed rows or lazy names outside the owned prefixes.
                foreach (var row in Names.ToArray())
                    if (!initial.ContainsKey(row.Key) && (Owned(row.Value) || row.Value == unrelated)) Names.Remove(row.Key);
            }
        }
    }
}
