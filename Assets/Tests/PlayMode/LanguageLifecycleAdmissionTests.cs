using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using Hardlight.Localisation;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // Admission and unwind controls for the Language lifecycle lease (issue #87).
    // They use detached SystemRef<Language>/SystemRef<ISystem> objects and a
    // test-owned dictionary only: they never read or alter the shared
    // ProcessManager registry, the Language singleton or any HLPropertyStore state,
    // and never create a Unity object, so they run in the shared complement.
    public sealed class LanguageLifecycleAdmissionTests
    {
        internal sealed class IsolatedRow
        {
            public readonly SystemRef SystemRef;
            public readonly IDictionary SystemRefDictionary = new Dictionary<string, object>();
            public IsolatedRow(SystemRef reference) { SystemRef = reference; }
        }

        private static string LanguageName => ProcessManager.GetDefaultName<Language>();

        [TestCase(false)]
        [TestCase(true)]
        public void LanguageObserverRow_RejectsWithoutInvokingOrConsuming(bool throwObservers)
        {
            string name = LanguageName;
            var untyped = new SystemRef(name, null);
            var typed = new SystemRef<Language>(name, null);
            var calls = new int[8];
            var observerFault = new InvalidOperationException("borrowed Language observer");
            Action<int> observed = index =>
            {
                ++calls[index];
                if (throwObservers) throw observerFault;
            };
            untyped.OnSystemStartup += value => observed(0);
            untyped.OnSystemShutdown += value => observed(1);
            untyped.InvokeOnValid(value => observed(2));
            untyped.InvokeOnValid(value => observed(3));
            typed.OnSystemStartup += value => observed(4);
            typed.OnSystemShutdown += value => observed(5);
            typed.InvokeOnValid(value => observed(6));
            typed.InvokeOnValid(value => observed(7));
            var row = new IsolatedRow(untyped);
            row.SystemRefDictionary.Add(name, typed);
            IDictionary cache = row.SystemRefDictionary;
            IDictionary registry = new Dictionary<string, object> { { name, row } };
            var beforeUntyped = new ReferenceSnapshot<ISystem>(untyped);
            var beforeTyped = new ReferenceSnapshot<Language>(typed);
            Assert.AreEqual(2, beforeUntyped.PendingCount, "Two deferred untyped actions are pending.");
            Assert.AreEqual(2, beforeTyped.PendingCount, "Two deferred typed actions are pending.");

            Assert.IsTrue(untyped.IsNull() && typed.IsNull(), "A value-only guard would admit this observer-bearing Language row.");
            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => new LifecycleSystemRowLease(registry, name));
            Assert.AreNotSame(observerFault, failure, "Admission rejects before any observer can run.");
            StringAssert.Contains("requires an absent system row", failure.Message);
            Assert.AreEqual(1, registry.Count);
            Assert.AreSame(row, registry[name]);
            Assert.AreSame(cache, row.SystemRefDictionary);
            Assert.AreEqual(1, cache.Count);
            Assert.AreSame(typed, cache[name]);
            CollectionAssert.AreEqual(new int[8], calls, "No startup, shutdown or deferred Language work runs or is consumed.");
            beforeUntyped.AssertUnchanged(untyped);
            beforeTyped.AssertUnchanged(typed);
        }

        [Test]
        public void LanguageRawKeyWithNullRow_RejectsBeforeReadingIt()
        {
            string name = LanguageName;
            IDictionary registry = new Dictionary<string, object> { { name, null } };
            StringAssert.Contains("requires an absent system row", Assert.Throws<InvalidOperationException>(() => new LifecycleSystemRowLease(registry, name)).Message);
            Assert.AreEqual(1, registry.Count);
            Assert.IsTrue(registry.Contains(name));
            Assert.IsNull(registry[name]);
        }

        [TestCase(false, false, false)]
        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, true, false)]
        [TestCase(false, true, true)]
        [TestCase(true, true, true)]
        public void LanguageCreatedRow_UnwindRetainsFirstCauseOrderAndOwnedRelease(bool failBody, bool failCleanup, bool failDiagnostic)
        {
            string name = LanguageName;
            var borrowed = new SystemRef("isolated unrelated Language neighbour", null);
            int foreignCalls = 0;
            borrowed.OnSystemStartup += value => ++foreignCalls;
            borrowed.OnSystemShutdown += value => ++foreignCalls;
            borrowed.InvokeOnValid(value => { ++foreignCalls; throw new InvalidOperationException("foreign deferred work"); });
            var borrowedRow = new IsolatedRow(borrowed);
            IDictionary registry = new Dictionary<string, object> { { borrowed.SystemName(), borrowedRow } };
            var beforeBorrowed = new ReferenceSnapshot<ISystem>(borrowed);

            var bodyFault = new InvalidOperationException("first Language body failure");
            var cleanupFault = new InvalidOperationException("owned Language cleanup failure");
            var diagnosticFault = new InvalidOperationException("owned Language cleanup diagnostic failure");
            var order = new List<string>();
            var reports = new List<string>();
            var lease = new LifecycleSystemRowLease(registry, name, message =>
            {
                order.Add("report");
                reports.Add(message);
                if (failDiagnostic) throw diagnosticFault;
            });
            int ownedCalls = 0;
            Action<ISystem> untypedShutdown = value => ++ownedCalls;
            Action<Language> typedShutdown = value => ++ownedCalls;
            SystemRef untyped = null;
            SystemRef<Language> typed = null;
            Action body = () =>
            {
                using (lease)
                {
                    untyped = new SystemRef(name, null);
                    var row = new IsolatedRow(untyped);
                    registry.Add(name, row);
                    lease.CaptureCreatedRow(untyped);
                    typed = new SystemRef<Language>(name, null);
                    row.SystemRefDictionary.Add(ProcessManager.GetDefaultName<Language>(), typed);
                    lease.CaptureTypedReference(typed);
                    untyped.OnSystemShutdown += untypedShutdown;
                    typed.OnSystemShutdown += typedShutdown;
                    try
                    {
                        order.Add("body");
                        if (failBody) throw bodyFault;
                    }
                    finally
                    {
                        lease.RunCleanup("detach-language-typed-shutdown", () => { order.Add("detach-language-typed-shutdown"); typed.OnSystemShutdown -= typedShutdown; });
                        lease.RunCleanup("owned-language-host-control", () => { order.Add("owned-language-host-control"); if (failCleanup) throw cleanupFault; });
                        lease.RunCleanup("detach-language-untyped-shutdown", () => { order.Add("detach-language-untyped-shutdown"); untyped.OnSystemShutdown -= untypedShutdown; });
                    }
                }
            };

            if (failBody) Assert.AreSame(bodyFault, Assert.Throws<InvalidOperationException>(() => body()), "Cleanup must keep the body failure object primary.");
            else Assert.DoesNotThrow(() => body());

            var expectedOrder = new List<string> { "body", "detach-language-typed-shutdown", "owned-language-host-control" };
            if (failCleanup) expectedOrder.Add("report");
            expectedOrder.Add("detach-language-untyped-shutdown");
            CollectionAssert.AreEqual(expectedOrder, order, "A failed cleanup step and its report do not skip later owned steps.");
            Assert.AreEqual(failCleanup ? 1 : 0, lease.CleanupFailures.Count);
            Assert.AreEqual(failDiagnostic ? 1 : 0, lease.DiagnosticFailures.Count);
            Assert.AreEqual(failCleanup ? 1 : 0, reports.Count);
            if (failCleanup)
            {
                Assert.AreSame(cleanupFault, lease.CleanupFailures[0].Failure);
                Assert.AreSame(cleanupFault, lease.CleanupFailure);
                Assert.AreEqual("owned-language-host-control", lease.CleanupFailures[0].Step);
                StringAssert.Contains("owned-language-host-control", reports[0]);
                if (!failBody)
                    Assert.Throws<AssertionException>(() => Assert.IsEmpty(lease.CleanupFailures), "Normal completion rejects a retained cleanup failure.");
            }
            else Assert.IsNull(lease.CleanupFailure);
            if (failDiagnostic)
            {
                Assert.AreSame(diagnosticFault, lease.DiagnosticFailures[0].Failure);
                Assert.AreSame(diagnosticFault, lease.CleanupDiagnosticFailure);
                Assert.AreEqual("owned-language-host-control", lease.DiagnosticFailures[0].Step);
            }
            else Assert.IsNull(lease.CleanupDiagnosticFailure);

            Assert.IsFalse(registry.Contains(name), "Only the created, cleared Language row is released.");
            Assert.AreEqual(1, registry.Count);
            Assert.AreSame(borrowedRow, registry[borrowed.SystemName()]);
            Assert.AreEqual(0, foreignCalls);
            Assert.AreEqual(0, ownedCalls, "No Language host was registered; owned observers never run.");
            beforeBorrowed.AssertUnchanged(borrowed);
            lease.Dispose();
            Assert.AreEqual(failCleanup ? 1 : 0, lease.CleanupFailures.Count, "Disposal does not retry or replace recorded failures.");
            Assert.AreEqual(failDiagnostic ? 1 : 0, lease.DiagnosticFailures.Count);
        }

        // Read-only snapshot of all six reference fields and the FastAction
        // active flag, list and queue identities and contents.
        internal sealed class ReferenceSnapshot<T> where T : class, ISystem
        {
            private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            private static readonly string[] Names = { "OnSystemStartup", "OnSystemShutdown", "m_actionOnSystemValid", "m_systemName", "m_system", "m_isystem" };
            private static readonly string[] QueueNames = { "m_invocationList", "m_removedList", "m_addedList" };
            private readonly object[] values = new object[Names.Length];
            private readonly object invocationActive;
            private readonly object[] queueIdentities = new object[QueueNames.Length];
            private readonly object[][] queueItems = new object[QueueNames.Length][];

            internal int PendingCount => queueItems[0] == null ? 0 : queueItems[0].Length;

            internal ReferenceSnapshot(SystemRef<T> reference)
            {
                for (int index = 0; index < Names.Length; ++index)
                    values[index] = Read(typeof(SystemRef<T>), Names[index], reference);
                object pending = values[2];
                if (pending == null) return;
                Type actionBase = pending.GetType().BaseType;
                invocationActive = Read(actionBase, "m_invocationActive", pending);
                for (int index = 0; index < QueueNames.Length; ++index)
                {
                    var queue = (IList)Read(actionBase, QueueNames[index], pending);
                    queueIdentities[index] = queue;
                    queueItems[index] = new object[queue.Count];
                    queue.CopyTo(queueItems[index], 0);
                }
            }

            internal void AssertUnchanged(SystemRef<T> reference)
            {
                for (int index = 0; index < Names.Length; ++index)
                    Assert.AreSame(values[index], Read(typeof(SystemRef<T>), Names[index], reference), Names[index]);
                object pending = values[2];
                if (pending == null) return;
                Type actionBase = pending.GetType().BaseType;
                Assert.AreEqual(invocationActive, Read(actionBase, "m_invocationActive", pending));
                for (int index = 0; index < QueueNames.Length; ++index)
                {
                    var queue = (IList)Read(actionBase, QueueNames[index], pending);
                    Assert.AreSame(queueIdentities[index], queue, QueueNames[index]);
                    Assert.AreEqual(queueItems[index].Length, queue.Count, QueueNames[index]);
                    for (int item = 0; item < queue.Count; ++item)
                        Assert.AreSame(queueItems[index][item], queue[item], QueueNames[index] + "/" + item);
                }
            }

            private static object Read(Type type, string name, object instance)
            {
                FieldInfo field = type.GetField(name, Fields);
                if (field == null) throw new InvalidOperationException("Original field not found: " + type + "." + name);
                return field.GetValue(instance);
            }
        }
    }
}
