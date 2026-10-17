using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using Hardlight.Utils;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    public sealed class LifecycleSystemRowLeaseTests
    {
        // Isolated registry shape: these tests never retrieve or alter the shared
        // ProcessManager dictionary and never create an original Unity host.
        private sealed class IsolatedRow
        {
            public readonly SystemRef SystemRef;
            public readonly IDictionary SystemRefDictionary = new Dictionary<string, object>();
            public IsolatedRow(SystemRef reference) { SystemRef = reference; }
        }

        private sealed class FixtureSystem : ISystem { }
        private sealed class OtherFixtureSystem : ISystem { }

        [TestCase(false)]
        [TestCase(true)]
        public void CoroutineHostNullRow_RejectsObserversWithoutInvokingOrConsuming(bool throwObservers) =>
            CheckBorrowedNullRow<CoroutineUtils>(throwObservers);

        [Test]
        public void RawKeyWithNullRow_RejectsBeforeReadingItsReferences()
        {
            const string name = "isolated raw null row";
            IDictionary registry = new Dictionary<string, object> { { name, null } };
            StringAssert.Contains("requires an absent system row", Assert.Throws<InvalidOperationException>(() => new LifecycleSystemRowLease(registry, name)).Message);
            Assert.IsTrue(registry.Contains(name));
            Assert.IsNull(registry[name]);
        }

        private static void CheckBorrowedNullRow<T>(bool throwObservers) where T : class, ISystem
        {
            string name = ProcessManager.GetDefaultName<T>();
            var untyped = new SystemRef(name, null);
            var typed = new SystemRef<T>(name, null);
            var calls = new int[6];
            var observerFault = new InvalidOperationException("borrowed lifecycle observer");
            Action<int> observed = index =>
            {
                ++calls[index];
                if (throwObservers) throw observerFault;
            };
            untyped.OnSystemStartup += value => observed(0);
            untyped.OnSystemShutdown += value => observed(1);
            untyped.InvokeOnValid(value => observed(2));
            typed.OnSystemStartup += value => observed(3);
            typed.OnSystemShutdown += value => observed(4);
            typed.InvokeOnValid(value => observed(5));
            var row = new IsolatedRow(untyped);
            row.SystemRefDictionary.Add(ProcessManager.GetDefaultName<T>(), typed);
            IDictionary registry = new Dictionary<string, object> { { name, row } };
            var beforeUntyped = new ReferenceSnapshot<ISystem>(untyped);
            var beforeTyped = new ReferenceSnapshot<T>(typed);

            Assert.IsTrue(untyped.IsNull() && typed.IsNull(), "The old value-only guard admitted this observer-bearing row.");
            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => new LifecycleSystemRowLease(registry, name));
            Assert.AreNotSame(observerFault, failure, "Admission must fail before any throwing observer is invoked.");
            StringAssert.Contains("requires an absent system row", failure.Message);
            Assert.AreEqual(1, registry.Count);
            Assert.AreSame(row, registry[name]);
            Assert.AreSame(typed, row.SystemRefDictionary[ProcessManager.GetDefaultName<T>()]);
            CollectionAssert.AreEqual(new int[6], calls, "Neither startup, shutdown nor deferred work belongs to the fixture.");
            beforeUntyped.AssertUnchanged(untyped);
            beforeTyped.AssertUnchanged(typed);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CreatedNullRow_OnlyItsOwnClearedRowIsRemovedOnSuccessOrFailure(bool failBody)
        {
            const string name = "isolated fixture lifecycle";
            var borrowed = new SystemRef("isolated unrelated row", null);
            int foreignCalls = 0;
            borrowed.OnSystemStartup += value => ++foreignCalls;
            borrowed.OnSystemShutdown += value => ++foreignCalls;
            borrowed.InvokeOnValid(value => ++foreignCalls);
            var borrowedRow = new IsolatedRow(borrowed);
            IDictionary registry = new Dictionary<string, object> { { borrowed.SystemName(), borrowedRow } };
            var beforeBorrowed = new ReferenceSnapshot<ISystem>(borrowed);
            var fixtureFault = new InvalidOperationException("owned fixture body");
            var lease = new LifecycleSystemRowLease(registry, name);
            Action body = () =>
            {
                using (lease)
                {
                    var untyped = new SystemRef(name, null);
                    var row = new IsolatedRow(untyped);
                    registry.Add(name, row);
                    lease.CaptureCreatedRow(untyped);
                    var typed = new SystemRef<FixtureSystem>(name, null);
                    row.SystemRefDictionary.Add(ProcessManager.GetDefaultName<FixtureSystem>(), typed);
                    lease.CaptureTypedReference(typed);
                    Action<FixtureSystem> ownedShutdown = value => Assert.Fail("No host is registered in this isolated lease test.");
                    typed.OnSystemShutdown += ownedShutdown;
                    try
                    {
                        if (failBody) throw fixtureFault;
                    }
                    finally { lease.RunCleanup("detach-isolated-owned-shutdown", () => typed.OnSystemShutdown -= ownedShutdown); }
                }
            };
            if (failBody) Assert.AreSame(fixtureFault, Assert.Throws<InvalidOperationException>(() => body()));
            else Assert.DoesNotThrow(() => body());
            Assert.IsNull(lease.CleanupFailure);
            Assert.IsNull(lease.CleanupDiagnosticFailure);
            Assert.IsFalse(registry.Contains(name), "Only the created, cleared row is removed after cleanup.");
            Assert.AreEqual(1, registry.Count);
            Assert.AreSame(borrowedRow, registry[borrowed.SystemName()]);
            Assert.AreEqual(0, foreignCalls);
            beforeBorrowed.AssertUnchanged(borrowed);
            Assert.DoesNotThrow(lease.Dispose, "Successful cleanup is idempotent.");
        }

        [Test]
        public void CreatedUntypedNullRow_CanBeReleasedBeforeTypedAcquisition()
        {
            const string name = "isolated untyped lifecycle";
            IDictionary registry = new Dictionary<string, object>();
            using (var lease = new LifecycleSystemRowLease(registry, name))
            {
                var untyped = new SystemRef(name, null);
                registry.Add(name, new IsolatedRow(untyped));
                lease.CaptureCreatedRow(untyped);
            }
            Assert.IsEmpty(registry);
        }

        [TestCase(false, "startup")]
        [TestCase(false, "shutdown")]
        [TestCase(false, "deferred")]
        [TestCase(true, "startup")]
        [TestCase(true, "shutdown")]
        [TestCase(true, "deferred")]
        public void CreatedRow_GainingUnownedWorkIsRetainedWithoutInvokingIt(bool typedObserver, string observerKind)
        {
            const string name = "isolated changed lifecycle";
            IDictionary registry = new Dictionary<string, object>();
            var lease = new LifecycleSystemRowLease(registry, name);
            var untyped = new SystemRef(name, null);
            var row = new IsolatedRow(untyped);
            registry.Add(name, row);
            lease.CaptureCreatedRow(untyped);
            var typed = new SystemRef<FixtureSystem>(name, null);
            row.SystemRefDictionary.Add(ProcessManager.GetDefaultName<FixtureSystem>(), typed);
            lease.CaptureTypedReference(typed);
            int foreignCalls = 0;
            Action<ISystem> untypedWork = value => { ++foreignCalls; throw new InvalidOperationException("foreign work"); };
            Action<FixtureSystem> typedWork = value => { ++foreignCalls; throw new InvalidOperationException("foreign work"); };
            if (typedObserver) AttachObserver(typed, observerKind, typedWork);
            else AttachObserver(untyped, observerKind, untypedWork);
            var beforeUntyped = new ReferenceSnapshot<ISystem>(untyped);
            var beforeTyped = new ReferenceSnapshot<FixtureSystem>(typed);

            Assert.DoesNotThrow(lease.Dispose);
            Assert.IsInstanceOf<InvalidOperationException>(lease.CleanupFailure);
            StringAssert.Contains("retains unowned state", lease.CleanupFailure.Message);
            Assert.AreSame(row, registry[name]);
            Assert.AreEqual(0, foreignCalls);
            beforeUntyped.AssertUnchanged(untyped);
            beforeTyped.AssertUnchanged(typed);
        }

        // The typed cache can change after capture without touching the captured
        // reference: its entry may be swapped, or a second typed entry may appear.
        // Both carry foreign work, so release must keep the row instead of
        // checking only the references it captured (LifecycleSystemRowLease.cs:105,110).
        [TestCase(false)]
        [TestCase(true)]
        public void CreatedRow_ReplacedOrExtraTypedEntryIsRetainedWithoutInvokingIt(bool replaceCapturedEntry)
        {
            const string name = "isolated changed typed cache";
            IDictionary registry = new Dictionary<string, object>();
            var lease = new LifecycleSystemRowLease(registry, name);
            var untyped = new SystemRef(name, null);
            var row = new IsolatedRow(untyped);
            registry.Add(name, row);
            lease.CaptureCreatedRow(untyped);
            var typed = new SystemRef<FixtureSystem>(name, null);
            row.SystemRefDictionary.Add(ProcessManager.GetDefaultName<FixtureSystem>(), typed);
            lease.CaptureTypedReference(typed);
            int foreignCalls = 0;
            Action<OtherFixtureSystem> foreignWork = value => { ++foreignCalls; throw new InvalidOperationException("foreign typed work"); };
            // Either the captured key now maps to a different reference, or an
            // uncaptured key was added; each new reference holds deferred work.
            string foreignKey = replaceCapturedEntry ? ProcessManager.GetDefaultName<FixtureSystem>() : ProcessManager.GetDefaultName<OtherFixtureSystem>();
            var foreign = new SystemRef<OtherFixtureSystem>(name, null);
            foreign.InvokeOnValid(foreignWork);
            row.SystemRefDictionary[foreignKey] = foreign;
            var beforeTyped = new ReferenceSnapshot<FixtureSystem>(typed);
            var beforeForeign = new ReferenceSnapshot<OtherFixtureSystem>(foreign);

            Assert.DoesNotThrow(lease.Dispose);
            Assert.IsInstanceOf<InvalidOperationException>(lease.CleanupFailure);
            StringAssert.Contains(replaceCapturedEntry ? "typed cache ownership changed" : "row ownership changed", lease.CleanupFailure.Message);
            Assert.AreSame(row, registry[name], "A changed typed cache keeps the row and its foreign work.");
            Assert.AreSame(foreign, row.SystemRefDictionary[foreignKey]);
            Assert.AreEqual(replaceCapturedEntry ? 1 : 2, row.SystemRefDictionary.Count);
            Assert.AreEqual(0, foreignCalls);
            beforeTyped.AssertUnchanged(typed);
            beforeForeign.AssertUnchanged(foreign);
        }

        // AdmitAbsentOrIdle: an earlier static initializer (for example
        // PerformanceDependency..cctor, x86-64 0x1b10280) can leave an idle row
        // and typed reference for the whole run. Borrowing keeps that row and
        // its prior entry, removes only the entry added while borrowed, and
        // returns the prior entry to the same idle state.
        [TestCase(false)]
        [TestCase(true)]
        public void IdleRow_IsBorrowedAndOnlyItsAddedTypedEntryIsRemoved(bool failBody)
        {
            const string name = "isolated idle borrowed row";
            var untyped = new SystemRef(name, null);
            var row = new IsolatedRow(untyped);
            var prior = new SystemRef<FixtureSystem>(name, null);
            row.SystemRefDictionary.Add(ProcessManager.GetDefaultName<FixtureSystem>(), prior);
            IDictionary registry = new Dictionary<string, object> { { name, row } };
            var beforePrior = new ReferenceSnapshot<FixtureSystem>(prior);
            var fixtureFault = new InvalidOperationException("borrowed fixture body");
            LifecycleSystemRowLease lease = LifecycleSystemRowLease.AdmitAbsentOrIdle(registry, name);
            Assert.IsTrue(lease.IsBorrowed);
            Action body = () =>
            {
                using (lease)
                {
                    lease.CaptureCreatedRow(untyped);
                    lease.CaptureTypedReference(prior);
                    var added = new SystemRef<OtherFixtureSystem>(name, null);
                    row.SystemRefDictionary.Add(ProcessManager.GetDefaultName<OtherFixtureSystem>(), added);
                    lease.CaptureTypedReference(added);
                    Action<FixtureSystem> ownedStartup = value => Assert.Fail("No host is registered in this isolated lease test.");
                    prior.OnSystemStartup += ownedStartup;
                    try
                    {
                        if (failBody) throw fixtureFault;
                    }
                    finally { lease.RunCleanup("detach-borrowed-startup", () => prior.OnSystemStartup -= ownedStartup); }
                }
            };
            if (failBody) Assert.AreSame(fixtureFault, Assert.Throws<InvalidOperationException>(() => body()));
            else Assert.DoesNotThrow(() => body());
            Assert.IsNull(lease.CleanupFailure);
            Assert.IsNull(lease.CleanupDiagnosticFailure);
            Assert.AreSame(row, registry[name], "A borrowed row is never removed.");
            Assert.AreEqual(1, row.SystemRefDictionary.Count, "Only the entry added while borrowed is removed.");
            Assert.AreSame(prior, row.SystemRefDictionary[ProcessManager.GetDefaultName<FixtureSystem>()]);
            beforePrior.AssertUnchanged(prior);
            Assert.DoesNotThrow(lease.Dispose, "Successful release is idempotent.");
            Assert.AreSame(row, registry[name]);
        }

        // Admission must refuse a row whose untyped or typed reference holds a
        // system, an observer or deferred work, reading fields only, so the
        // issue-87 guarantee holds for borrowed rows too. A raw null row is
        // also refused. A lease that admitted any of these fails.
        [TestCase(false, "startup")]
        [TestCase(false, "shutdown")]
        [TestCase(false, "deferred")]
        [TestCase(false, "system")]
        [TestCase(true, "startup")]
        [TestCase(true, "shutdown")]
        [TestCase(true, "deferred")]
        [TestCase(true, "system")]
        public void ObservedOrOwnedRow_IsNotBorrowedAndItsWorkIsNotInvoked(bool typedObserver, string observerKind)
        {
            const string name = "isolated observed borrowed row";
            int foreignCalls = 0;
            Action<ISystem> untypedWork = value => { ++foreignCalls; throw new InvalidOperationException("foreign work"); };
            Action<FixtureSystem> typedWork = value => { ++foreignCalls; throw new InvalidOperationException("foreign work"); };
            bool owned = observerKind == "system";
            var untyped = new SystemRef(name, owned && !typedObserver ? new FixtureSystem() : null);
            var typed = new SystemRef<FixtureSystem>(name, owned && typedObserver ? new FixtureSystem() : null);
            if (!owned)
            {
                if (typedObserver) AttachObserver(typed, observerKind, typedWork);
                else AttachObserver(untyped, observerKind, untypedWork);
            }
            var row = new IsolatedRow(untyped);
            row.SystemRefDictionary.Add(ProcessManager.GetDefaultName<FixtureSystem>(), typed);
            IDictionary registry = new Dictionary<string, object> { { name, row } };
            var beforeUntyped = new ReferenceSnapshot<ISystem>(untyped);
            var beforeTyped = new ReferenceSnapshot<FixtureSystem>(typed);

            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => LifecycleSystemRowLease.AdmitAbsentOrIdle(registry, name));
            StringAssert.Contains("retains unowned state", failure.Message);
            Assert.AreSame(row, registry[name]);
            Assert.AreSame(typed, row.SystemRefDictionary[ProcessManager.GetDefaultName<FixtureSystem>()]);
            Assert.AreEqual(0, foreignCalls);
            beforeUntyped.AssertUnchanged(untyped);
            beforeTyped.AssertUnchanged(typed);

            IDictionary nullRow = new Dictionary<string, object> { { name, null } };
            StringAssert.Contains("requires an absent or idle system row",
                Assert.Throws<InvalidOperationException>(() => LifecycleSystemRowLease.AdmitAbsentOrIdle(nullRow, name)).Message);
            Assert.IsNull(nullRow[name]);
        }

        // Work gained while borrowed belongs to someone else: the prior or
        // added entry is checked, never cleared, and the row and that entry stay.
        [TestCase(false)]
        [TestCase(true)]
        public void BorrowedRow_GainingUnownedWorkIsRetainedWithoutInvokingIt(bool onPriorEntry)
        {
            const string name = "isolated changed borrowed row";
            var untyped = new SystemRef(name, null);
            var row = new IsolatedRow(untyped);
            var prior = new SystemRef<FixtureSystem>(name, null);
            row.SystemRefDictionary.Add(ProcessManager.GetDefaultName<FixtureSystem>(), prior);
            IDictionary registry = new Dictionary<string, object> { { name, row } };
            LifecycleSystemRowLease lease = LifecycleSystemRowLease.AdmitAbsentOrIdle(registry, name);
            lease.CaptureCreatedRow(untyped);
            var added = new SystemRef<OtherFixtureSystem>(name, null);
            row.SystemRefDictionary.Add(ProcessManager.GetDefaultName<OtherFixtureSystem>(), added);
            lease.CaptureTypedReference(added);
            int foreignCalls = 0;
            if (onPriorEntry) prior.InvokeOnValid(value => { ++foreignCalls; throw new InvalidOperationException("foreign deferred work"); });
            else added.InvokeOnValid(value => { ++foreignCalls; throw new InvalidOperationException("foreign deferred work"); });
            var beforePrior = new ReferenceSnapshot<FixtureSystem>(prior);
            var beforeAdded = new ReferenceSnapshot<OtherFixtureSystem>(added);

            Assert.DoesNotThrow(lease.Dispose);
            Assert.IsInstanceOf<InvalidOperationException>(lease.CleanupFailure);
            StringAssert.Contains("retains unowned state", lease.CleanupFailure.Message);
            Assert.AreSame(row, registry[name]);
            Assert.AreEqual(2, row.SystemRefDictionary.Count, "The added entry with foreign work is retained.");
            Assert.AreSame(added, row.SystemRefDictionary[ProcessManager.GetDefaultName<OtherFixtureSystem>()]);
            Assert.AreEqual(0, foreignCalls);
            beforePrior.AssertUnchanged(prior);
            beforeAdded.AssertUnchanged(added);
        }

        [Test]
        public void BodyFailureAndUnownedPendingWork_RetainFirstCauseAndCleanupFailure()
        {
            const string name = "isolated faulted lifecycle";
            IDictionary registry = new Dictionary<string, object>();
            var lease = new LifecycleSystemRowLease(registry, name);
            var untyped = new SystemRef(name, null);
            var row = new IsolatedRow(untyped);
            registry.Add(name, row);
            lease.CaptureCreatedRow(untyped);
            int foreignCalls = 0;
            untyped.InvokeOnValid(value => { ++foreignCalls; throw new InvalidOperationException("foreign deferred work"); });
            var before = new ReferenceSnapshot<ISystem>(untyped);
            var bodyFault = new InvalidOperationException("first fixture body failure");

            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
            {
                using (lease) { throw bodyFault; }
            });
            Assert.AreSame(bodyFault, failure, "Cleanup must retain the earlier failure object.");
            Assert.IsInstanceOf<InvalidOperationException>(lease.CleanupFailure);
            Assert.AreNotSame(bodyFault, lease.CleanupFailure);
            StringAssert.Contains("retains unowned state", lease.CleanupFailure.Message);
            Assert.AreSame(row, registry[name], "Failed cleanup must retain the row and unowned pending work.");
            Assert.AreEqual(0, foreignCalls);
            before.AssertUnchanged(untyped);
        }

        [TestCase(0, false)]
        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(0, true)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        public void PreDisposeCleanupAndDiagnosticFault_ContinueLaterOwnedStepsAndRetainFirstCause(int faultStep, bool failBody)
        {
            const string name = "isolated pre-disposal cleanup";
            var borrowed = new SystemRef("isolated unrelated pending work", null);
            int foreignCalls = 0;
            borrowed.OnSystemStartup += value => ++foreignCalls;
            borrowed.OnSystemShutdown += value => ++foreignCalls;
            borrowed.InvokeOnValid(value => { ++foreignCalls; throw new InvalidOperationException("foreign deferred work"); });
            var borrowedRow = new IsolatedRow(borrowed);
            IDictionary registry = new Dictionary<string, object> { { borrowed.SystemName(), borrowedRow } };
            var beforeBorrowed = new ReferenceSnapshot<ISystem>(borrowed);
            var bodyFault = new InvalidOperationException("first body failure");
            var cleanupFault = new InvalidOperationException("owned pre-disposal cleanup failure");
            var diagnosticFault = new InvalidOperationException("owned cleanup diagnostic failure");
            var reports = new List<string>();
            var lease = new LifecycleSystemRowLease(registry, name, message => { reports.Add(message); throw diagnosticFault; });
            var untyped = new SystemRef(name, null);
            var row = new IsolatedRow(untyped);
            registry.Add(name, row);
            lease.CaptureCreatedRow(untyped);
            var typed = new SystemRef<FixtureSystem>(name, null);
            row.SystemRefDictionary.Add(ProcessManager.GetDefaultName<FixtureSystem>(), typed);
            lease.CaptureTypedReference(typed);
            Action<FixtureSystem> ownedShutdown = value => Assert.Fail("No Unity host exists in this cleanup control.");
            typed.OnSystemShutdown += ownedShutdown;
            var order = new List<string>();
            string[] steps = { "owned-listener-control", "owned-object-control", "owned-restoration-control" };
            var restored = new bool[steps.Length];
            Action body = () =>
            {
                using (lease)
                {
                    try { if (failBody) throw bodyFault; }
                    finally
                    {
                        for (int index = 0; index < steps.Length; ++index)
                        {
                            int current = index;
                            lease.RunCleanup(steps[current], () =>
                            {
                                order.Add(steps[current]);
                                if (current == faultStep) throw cleanupFault;
                                restored[current] = true;
                            });
                        }
                        lease.RunCleanup("detach-owned-control-shutdown", () => typed.OnSystemShutdown -= ownedShutdown);
                    }
                }
            };

            if (failBody) Assert.AreSame(bodyFault, Assert.Throws<InvalidOperationException>(() => body()));
            else Assert.DoesNotThrow(() => body());
            CollectionAssert.AreEqual(steps, order, "A failed early cleanup must not skip later owned steps.");
            for (int index = 0; index < restored.Length; ++index) Assert.AreEqual(index != faultStep, restored[index]);
            Assert.AreEqual(1, lease.CleanupFailures.Count);
            Assert.AreSame(cleanupFault, lease.CleanupFailures[0].Failure);
            Assert.AreEqual(steps[faultStep], lease.CleanupFailures[0].Step);
            Assert.AreEqual(1, lease.DiagnosticFailures.Count);
            Assert.AreSame(diagnosticFault, lease.DiagnosticFailures[0].Failure);
            Assert.AreEqual(steps[faultStep], lease.DiagnosticFailures[0].Step);
            Assert.AreEqual(1, reports.Count);
            StringAssert.Contains(steps[faultStep], reports[0]);
            Assert.IsFalse(registry.Contains(name), "Earlier cleanup failure must not skip safe release of the cleared owned row.");
            Assert.AreSame(borrowedRow, registry[borrowed.SystemName()]);
            Assert.AreEqual(0, foreignCalls);
            beforeBorrowed.AssertUnchanged(borrowed);
            if (!failBody) Assert.Throws<AssertionException>(() => Assert.IsEmpty(lease.CleanupFailures), "Normal completion must reject retained cleanup failure.");
            lease.Dispose();
            Assert.AreEqual(1, lease.CleanupFailures.Count, "Disposal does not retry or replace recorded failures.");
            Assert.AreEqual(1, lease.DiagnosticFailures.Count);
        }

        [Test]
        public void MultiplePreDisposeCleanupFailures_RetainActualObjectsAndContinueRestoration()
        {
            IDictionary registry = new Dictionary<string, object>();
            var bodyFault = new InvalidOperationException("first body failure");
            var cleanupFaults = new[] { new InvalidOperationException("first owned cleanup"), new InvalidOperationException("second owned cleanup") };
            var diagnosticFaults = new[] { new InvalidOperationException("first owned diagnostic"), new InvalidOperationException("second owned diagnostic") };
            int reports = 0;
            var lease = new LifecycleSystemRowLease(registry, "isolated multiple cleanup", message => { throw diagnosticFaults[reports++]; });
            bool restored = false;
            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
            {
                using (lease)
                {
                    try { throw bodyFault; }
                    finally
                    {
                        lease.RunCleanup("first-owned-step", () => { throw cleanupFaults[0]; });
                        lease.RunCleanup("second-owned-step", () => { throw cleanupFaults[1]; });
                        lease.RunCleanup("later-owned-restoration", () => restored = true);
                    }
                }
            });
            Assert.AreSame(bodyFault, failure);
            Assert.IsTrue(restored);
            Assert.AreEqual(2, lease.CleanupFailures.Count);
            Assert.AreEqual(2, lease.DiagnosticFailures.Count);
            Assert.AreEqual(2, reports);
            for (int index = 0; index < cleanupFaults.Length; ++index)
            {
                Assert.AreSame(cleanupFaults[index], lease.CleanupFailures[index].Failure);
                Assert.AreSame(diagnosticFaults[index], lease.DiagnosticFailures[index].Failure);
                Assert.AreEqual(lease.CleanupFailures[index].Step, lease.DiagnosticFailures[index].Step);
            }
            Assert.AreSame(cleanupFaults[0], lease.CleanupFailure);
            Assert.AreSame(diagnosticFaults[0], lease.CleanupDiagnosticFailure);
            Assert.IsEmpty(registry);
        }

        private static void AttachObserver<T>(SystemRef<T> reference, string kind, Action<T> observer) where T : class, ISystem
        {
            if (kind == "startup") reference.OnSystemStartup += observer;
            else if (kind == "shutdown") reference.OnSystemShutdown += observer;
            else reference.InvokeOnValid(observer);
        }

        // CaptureTypedReferenceIfCreated: the failure-path capture used by the
        // cursor fixture. It must adopt only an entry already in this lease's own
        // row, never create one, and never invoke or remove foreign work.
        private static LifecycleSystemRowLease CapturedRow(IDictionary registry, string name, List<string> reports, out IsolatedRow row)
        {
            var lease = new LifecycleSystemRowLease(registry, name, reports.Add);
            var untyped = new SystemRef(name, null);
            row = new IsolatedRow(untyped);
            registry.Add(name, row);
            lease.CaptureCreatedRow(untyped);
            return lease;
        }

        [Test]
        public void CaptureIfCreated_ReleasesAnUncapturedConstructorEntryAfterBodyFailureWithoutCreatingOne()
        {
            const string name = "isolated partial constructor";
            string key = ProcessManager.GetDefaultName<FixtureSystem>();
            IDictionary registry = new Dictionary<string, object>();
            var reports = new List<string>();
            LifecycleSystemRowLease lease = CapturedRow(registry, name, reports, out IsolatedRow row);
            // Left by a constructor that failed before its success-path capture.
            var typed = new SystemRef<FixtureSystem>(name, null);
            row.SystemRefDictionary.Add(key, typed);
            var bodyFault = new InvalidOperationException("owned partial acquisition");
            Assert.AreSame(bodyFault, Assert.Throws<InvalidOperationException>(() =>
            {
                using (lease)
                {
                    try { throw bodyFault; }
                    finally
                    {
                        lease.RunCleanup("capture-partial-typed-reference", lease.CaptureTypedReferenceIfCreated<FixtureSystem>);
                        Assert.AreEqual(1, row.SystemRefDictionary.Count, "The capture creates no reference.");
                        Assert.AreSame(typed, row.SystemRefDictionary[key]);
                    }
                }
            }));
            Assert.IsEmpty(lease.CleanupFailures);
            Assert.IsEmpty(reports);
            Assert.IsFalse(registry.Contains(name), "The adopted entry lets the owned row be released.");
        }

        [Test]
        public void CaptureIfCreated_UncapturedRowAndAbsentEntryAreNoOpsAndCreateNothing()
        {
            const string name = "isolated absent typed entry";
            IDictionary registry = new Dictionary<string, object>();
            var reports = new List<string>();
            var early = new LifecycleSystemRowLease(registry, name, reports.Add);
            Assert.DoesNotThrow(early.CaptureTypedReferenceIfCreated<FixtureSystem>, "No captured row: nothing to adopt.");
            Assert.IsEmpty(registry);
            early.Dispose();
            LifecycleSystemRowLease lease = CapturedRow(registry, name, reports, out IsolatedRow row);
            Assert.DoesNotThrow(lease.CaptureTypedReferenceIfCreated<FixtureSystem>);
            Assert.AreEqual(0, row.SystemRefDictionary.Count, "An absent entry is never created.");
            lease.Dispose();
            Assert.IsEmpty(lease.CleanupFailures);
            Assert.IsEmpty(reports);
            Assert.IsEmpty(registry);
        }

        [Test]
        public void CaptureIfCreated_AlreadyCapturedEntryIsANoOp()
        {
            const string name = "isolated captured typed entry";
            IDictionary registry = new Dictionary<string, object>();
            var reports = new List<string>();
            LifecycleSystemRowLease lease = CapturedRow(registry, name, reports, out IsolatedRow row);
            var typed = new SystemRef<FixtureSystem>(name, null);
            row.SystemRefDictionary.Add(ProcessManager.GetDefaultName<FixtureSystem>(), typed);
            lease.CaptureTypedReference(typed);
            Assert.DoesNotThrow(lease.CaptureTypedReferenceIfCreated<FixtureSystem>);
            lease.Dispose();
            Assert.IsEmpty(lease.CleanupFailures);
            Assert.IsEmpty(reports);
            Assert.IsEmpty(registry);
        }

        [Test]
        public void CaptureIfCreated_ReplacedRowIsRefusedWithoutDeletionOrInvocationAndLaterCleanupContinues()
        {
            const string name = "isolated replaced row";
            IDictionary registry = new Dictionary<string, object>();
            var reports = new List<string>();
            LifecycleSystemRowLease lease = CapturedRow(registry, name, reports, out IsolatedRow row);
            row.SystemRefDictionary.Add(ProcessManager.GetDefaultName<FixtureSystem>(), new SystemRef<FixtureSystem>(name, null));
            // A foreign owner replaces the row and attaches its own work.
            var foreign = new SystemRef(name, null);
            int foreignCalls = 0;
            foreign.OnSystemShutdown += value => { ++foreignCalls; throw new InvalidOperationException("foreign work"); };
            var replacement = new IsolatedRow(foreign);
            registry[name] = replacement;
            var before = new ReferenceSnapshot<ISystem>(foreign);
            bool laterStepRan = false;
            lease.RunCleanup("capture-partial-typed-reference", lease.CaptureTypedReferenceIfCreated<FixtureSystem>);
            lease.RunCleanup("later-owned-step", () => laterStepRan = true);
            lease.Dispose();
            Assert.IsTrue(laterStepRan, "A refused capture does not stop later cleanup.");
            Assert.AreEqual(2, lease.CleanupFailures.Count);
            StringAssert.Contains("cannot capture its typed reference", lease.CleanupFailures[0].Failure.Message);
            StringAssert.Contains("row ownership changed", lease.CleanupFailures[1].Failure.Message);
            Assert.AreEqual(2, reports.Count);
            Assert.AreSame(replacement, registry[name], "The replacement row is never deleted.");
            Assert.AreEqual(0, foreignCalls);
            before.AssertUnchanged(foreign);
        }

        [Test]
        public void CaptureIfCreated_PendingForeignObserverIsNeitherInvokedNorDeleted()
        {
            const string name = "isolated observed typed entry";
            IDictionary registry = new Dictionary<string, object>();
            var reports = new List<string>();
            LifecycleSystemRowLease lease = CapturedRow(registry, name, reports, out IsolatedRow row);
            var typed = new SystemRef<FixtureSystem>(name, null);
            int foreignCalls = 0;
            typed.OnSystemShutdown += value => { ++foreignCalls; throw new InvalidOperationException("foreign observer"); };
            row.SystemRefDictionary.Add(ProcessManager.GetDefaultName<FixtureSystem>(), typed);
            var before = new ReferenceSnapshot<FixtureSystem>(typed);
            lease.RunCleanup("capture-partial-typed-reference", lease.CaptureTypedReferenceIfCreated<FixtureSystem>);
            lease.Dispose();
            Assert.AreEqual(1, lease.CleanupFailures.Count, "The adopted entry is checked, not cleared.");
            StringAssert.Contains("retains unowned state", lease.CleanupFailures[0].Failure.Message);
            Assert.AreSame(row, registry[name], "The observed row is retained.");
            Assert.AreEqual(0, foreignCalls);
            before.AssertUnchanged(typed);
        }

        private sealed class ReferenceSnapshot<T> where T : class, ISystem
        {
            private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            private static readonly string[] Names = { "OnSystemStartup", "OnSystemShutdown", "m_actionOnSystemValid", "m_systemName", "m_system", "m_isystem" };
            private readonly object[] values = new object[Names.Length];
            private readonly object invocationActive;
            private readonly object[] queueIdentities = new object[3];
            private readonly object[][] queueItems = new object[3][];
            private static readonly string[] QueueNames = { "m_invocationList", "m_removedList", "m_addedList" };

            internal ReferenceSnapshot(SystemRef<T> reference)
            {
                for (int index = 0; index < Names.Length; ++index)
                    values[index] = typeof(SystemRef<T>).GetField(Names[index], Fields).GetValue(reference);
                object pending = values[2];
                if (pending == null) return;
                Type actionBase = pending.GetType().BaseType;
                invocationActive = actionBase.GetField("m_invocationActive", Fields).GetValue(pending);
                for (int index = 0; index < QueueNames.Length; ++index)
                {
                    IList queue = (IList)actionBase.GetField(QueueNames[index], Fields).GetValue(pending);
                    queueIdentities[index] = queue;
                    queueItems[index] = new object[queue.Count];
                    queue.CopyTo(queueItems[index], 0);
                }
            }

            internal void AssertUnchanged(SystemRef<T> reference)
            {
                for (int index = 0; index < Names.Length; ++index)
                    Assert.AreSame(values[index], typeof(SystemRef<T>).GetField(Names[index], Fields).GetValue(reference), Names[index]);
                object pending = values[2];
                if (pending == null) return;
                Type actionBase = pending.GetType().BaseType;
                Assert.AreEqual(invocationActive, actionBase.GetField("m_invocationActive", Fields).GetValue(pending));
                for (int index = 0; index < QueueNames.Length; ++index)
                {
                    IList queue = (IList)actionBase.GetField(QueueNames[index], Fields).GetValue(pending);
                    Assert.AreSame(queueIdentities[index], queue, QueueNames[index]);
                    Assert.AreEqual(queueItems[index].Length, queue.Count, QueueNames[index]);
                    for (int item = 0; item < queue.Count; ++item)
                        Assert.AreSame(queueItems[index][item], queue[item], QueueNames[index] + "/" + item);
                }
            }
        }
    }
}
