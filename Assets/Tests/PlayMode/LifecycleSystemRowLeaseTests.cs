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
