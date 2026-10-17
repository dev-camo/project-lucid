using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Localisation;
using NUnit.Framework;
using IsolatedRow = ProjectLucid.Tests.LanguageLifecycleAdmissionTests.IsolatedRow;
using Table = Hardlight.Localisation.StringTable;

namespace ProjectLucid.Tests
{
    // Admission controls for the StringTable, LoggingHost and MenuBloom
    // lifecycle fixtures. Each uses detached references in a test-owned
    // dictionary, never the shared ProcessManager registry. A value-only
    // IsSystemNull guard admits every refused row below; registering into it
    // would run its foreign startup observers and consume its deferred work.
    public sealed class BorrowedLifecycleRowAdmissionTests
    {
        private static void AssertRefusedUntouched<T>(Func<IDictionary, object> admit, bool throwObservers) where T : class, ISystem
        {
            string name = ProcessManager.GetDefaultName<T>();
            var untyped = new SystemRef(name, null);
            var typed = new SystemRef<T>(name, null);
            var calls = new int[8];
            var observerFault = new InvalidOperationException("borrowed observer");
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
            var beforeUntyped = new LanguageLifecycleAdmissionTests.ReferenceSnapshot<ISystem>(untyped);
            var beforeTyped = new LanguageLifecycleAdmissionTests.ReferenceSnapshot<T>(typed);

            Assert.IsTrue(untyped.IsNull() && typed.IsNull(), "A value-only guard would admit this row.");
            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => admit(registry));
            Assert.AreNotSame(observerFault, failure, "Admission refuses before any observer can run.");
            StringAssert.Contains("retains unowned state", failure.Message);
            Assert.AreEqual(1, registry.Count, "Refusal creates no row.");
            Assert.AreSame(row, registry[name]);
            Assert.AreSame(cache, row.SystemRefDictionary);
            Assert.AreEqual(1, cache.Count);
            Assert.AreSame(typed, cache[name]);
            CollectionAssert.AreEqual(new int[8], calls, "No startup, shutdown or deferred work runs or is consumed.");
            beforeUntyped.AssertUnchanged(untyped);
            beforeTyped.AssertUnchanged(typed);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StringTableFixture_RefusesObservedTableRow(bool throwObservers) =>
            AssertRefusedUntouched<Table>(StringTableLifecycleTests.AdmitRows, throwObservers);

        [TestCase(false)]
        [TestCase(true)]
        public void StringTableFixture_RefusesObservedLanguageRow(bool throwObservers) =>
            AssertRefusedUntouched<Language>(StringTableLifecycleTests.AdmitRows, throwObservers);

        [TestCase(false)]
        [TestCase(true)]
        public void StringTableFixture_RefusesObservedLoggingHostRow(bool throwObservers) =>
            AssertRefusedUntouched<HLUnityCore>(StringTableLifecycleTests.AdmitRows, throwObservers);

        [TestCase(false)]
        [TestCase(true)]
        public void LoggingHostFixture_RefusesObservedLoggingHostRow(bool throwObservers) =>
            AssertRefusedUntouched<HLUnityCore>(LoggingHostTests.AdmitHostRow, throwObservers);

        [TestCase(false)]
        [TestCase(true)]
        public void MenuBloomFixture_RefusesObservedBloomRow(bool throwObservers) =>
            AssertRefusedUntouched<MenuBloomTransition>(OriginalMenuBloomLifecycleTests.AdmitBloomRow, throwObservers);

        // The refused third row must not leave the two leases already taken
        // holding anything: an idle earlier row stays exactly as it was.
        [Test]
        public void StringTableFixture_RefusalLeavesEarlierIdleRowsUnchanged()
        {
            string tableName = ProcessManager.GetDefaultName<Table>(), coreName = ProcessManager.GetDefaultName<HLUnityCore>();
            var idle = new IsolatedRow(new SystemRef(tableName, null));
            var idleTyped = new SystemRef<Table>(tableName, null);
            idle.SystemRefDictionary.Add(tableName, idleTyped);
            var observed = new SystemRef(coreName, null);
            int calls = 0;
            observed.InvokeOnValid(value => ++calls);
            IDictionary registry = new Dictionary<string, object> { { tableName, idle }, { coreName, new IsolatedRow(observed) } };
            var beforeIdle = new LanguageLifecycleAdmissionTests.ReferenceSnapshot<ISystem>(idle.SystemRef);
            var beforeIdleTyped = new LanguageLifecycleAdmissionTests.ReferenceSnapshot<Table>(idleTyped);

            StringAssert.Contains("retains unowned state", Assert.Throws<InvalidOperationException>(() => StringTableLifecycleTests.AdmitRows(registry)).Message);
            Assert.AreEqual(2, registry.Count);
            Assert.AreSame(idle, registry[tableName]);
            Assert.AreEqual(1, idle.SystemRefDictionary.Count);
            Assert.AreSame(idleTyped, idle.SystemRefDictionary[tableName]);
            Assert.AreEqual(0, calls);
            beforeIdle.AssertUnchanged(idle.SystemRef);
            beforeIdleTyped.AssertUnchanged(idleTyped);
        }

        // Positive control: absent rows are leased as the fixture's own and an
        // idle row is borrowed; releasing without any activation changes nothing.
        [Test]
        public void FixturesAdmitAbsentAndIdleRowsWithoutChangingThem()
        {
            string languageName = ProcessManager.GetDefaultName<Language>();
            var idle = new IsolatedRow(new SystemRef(languageName, null));
            IDictionary registry = new Dictionary<string, object> { { languageName, idle } };

            LifecycleSystemRowLease[] rows = StringTableLifecycleTests.AdmitRows(registry);
            Assert.AreEqual(3, rows.Length);
            Assert.IsFalse(rows[0].IsBorrowed, "The absent Table row is the fixture's own.");
            Assert.IsTrue(rows[1].IsBorrowed, "The idle Language row is borrowed.");
            Assert.IsFalse(rows[2].IsBorrowed, "The absent logging-host row is the fixture's own.");
            LifecycleSystemRowLease host = LoggingHostTests.AdmitHostRow(registry);
            LifecycleSystemRowLease bloom = OriginalMenuBloomLifecycleTests.AdmitBloomRow(registry);
            Assert.IsFalse(host.IsBorrowed || bloom.IsBorrowed);
            foreach (LifecycleSystemRowLease lease in new[] { rows[0], rows[1], rows[2], host, bloom })
            {
                lease.Dispose();
                Assert.IsEmpty(lease.CleanupFailures);
            }
            Assert.AreEqual(1, registry.Count);
            Assert.AreSame(idle, registry[languageName]);
            Assert.AreEqual(0, idle.SystemRefDictionary.Count);
        }

        // The fixtures take no reference to an absent row before activation, so
        // the original registration creates it through ProcessManager's
        // new-row branch; the lease then captures the raw entry. A still
        // absent key is left alone, and a found row with a typed cache it did
        // not create is refused and stays.
        [Test]
        public void CreatedRowIsCapturedFromTheRawEntryAfterActivation()
        {
            string name = ProcessManager.GetDefaultName<MenuBloomTransition>();
            IDictionary registry = new Dictionary<string, object>();
            LifecycleSystemRowLease lease = OriginalMenuBloomLifecycleTests.AdmitBloomRow(registry);
            lease.CaptureRowIfCreated();
            Assert.AreEqual(0, registry.Count, "Capture creates no row.");
            var created = new IsolatedRow(new SystemRef(name, null));
            registry.Add(name, created);
            lease.CaptureRowIfCreated();
            lease.CaptureRowIfCreated();
            lease.Dispose();
            Assert.IsEmpty(lease.CleanupFailures);
            Assert.AreEqual(0, registry.Count, "The captured created row is removed once cleared.");

            LifecycleSystemRowLease refused = OriginalMenuBloomLifecycleTests.AdmitBloomRow(registry);
            var foreign = new IsolatedRow(new SystemRef(name, null));
            var foreignTyped = new SystemRef<MenuBloomTransition>(name, null);
            foreign.SystemRefDictionary.Add(name, foreignTyped);
            registry.Add(name, foreign);
            StringAssert.Contains("unowned typed cache", Assert.Throws<InvalidOperationException>(refused.CaptureRowIfCreated).Message);
            refused.Dispose();
            Assert.IsEmpty(refused.CleanupFailures);
            Assert.AreSame(foreign, registry[name], "An uncaptured row is never removed.");
            Assert.AreSame(foreignTyped, foreign.SystemRefDictionary[name]);
        }
    }
}
