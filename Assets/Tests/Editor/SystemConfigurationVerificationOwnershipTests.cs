using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Editor
{
    // The configuration fixture registers and unregisters the default SystemConfiguration row.
    // ProcessManager.UnregisterSystemInternal (0x06000e39) unsubscribes the row's system from
    // every action and re-registration never restores them, so the fixture must refuse a live
    // configuration it does not own before any registry access, and must never register into
    // a retained dormant row whose references belong to someone else.
    public sealed class SystemConfigurationVerificationOwnershipTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static IDictionary Rows() => (IDictionary)typeof(ProcessManager)
            .GetField("s_systemDictionary", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

        private static IDictionary ActionRows() => (IDictionary)typeof(ProcessManager)
            .GetField("s_systemActionLookup", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

        // IDictionary's own enumerator yields DictionaryEntry; the generic one behind LINQ does not.
        private static List<DictionaryEntry> Entries(IDictionary dictionary)
        {
            var entries = new List<DictionaryEntry>();
            foreach (DictionaryEntry entry in dictionary) entries.Add(entry);
            return entries;
        }

        [Test]
        public void LiveForeignConfigurationIsRefusedBeforeAnyRegistryMutation()
        {
            IDictionary rows = Rows();
            IDictionary actions = ActionRows();
            object[] rowLayout = FSMManagerVerification.CaptureLayout(rows);
            object[] actionLayout = FSMManagerVerification.CaptureLayout(actions);
            var actionRowLayouts = actions.Values.Cast<IDictionary>()
                .Select(row => new KeyValuePair<IDictionary, object[]>(row, FSMManagerVerification.CaptureLayout(row))).ToList();
            string name = ProcessManager.GetDefaultName<SystemConfiguration>();
            try
            {
                // Any retained configuration row is set aside so the foreign row is test-owned.
                rows.Remove(name);
                var foreign = new SystemConfiguration();
                SystemRef reference = ProcessManager.RegisterSystem(foreign);
                int startups = 0, shutdowns = 0, updates = 0;
                reference.OnSystemStartup += system => startups++;
                reference.OnSystemShutdown += system => shutdowns++;
                foreign.SubscribeToAction(SystemAction.Update, context => updates++);
                object row = rows[name];
                var typedCache = (IDictionary)row.GetType().GetField("SystemRefDictionary", Instance).GetValue(row);
                List<object> cachedTypes = typedCache.Keys.Cast<object>().ToList();

                var failure = Assert.Throws<InvalidOperationException>(() => SystemConfigurationVerification.RunManaged());
                Assert.That(failure.Message, Is.EqualTo("configuration proof requires no live SystemConfiguration"));
                Assert.That(startups, Is.Zero, "foreign startup observers ran");
                Assert.That(shutdowns, Is.Zero, "foreign shutdown observers ran");
                Assert.That(rows[name], Is.SameAs(row));
                Assert.That(typedCache.Keys.Cast<object>(), Is.EqualTo(cachedTypes), "typed reference cache changed");
                Assert.That(reference.GetSafe(), Is.SameAs(foreign));
                foreign.ProcessSystemAction(SystemAction.Update);
                Assert.That(updates, Is.EqualTo(1), "foreign Update subscription was lost");
            }
            finally
            {
                foreach (KeyValuePair<IDictionary, object[]> row in actionRowLayouts) FSMManagerVerification.RestoreLayout(row.Key, row.Value);
                FSMManagerVerification.RestoreLayout(actions, actionLayout);
                FSMManagerVerification.RestoreLayout(rows, rowLayout);
            }
        }

        // Discriminating control: a dormant row (null system) holding foreign observers and a typed
        // reference passes the live-system guard. The fixture must set it aside rather than register
        // into it, because RegisterSystem replaces every reference of an existing row (0x06000e34)
        // and the following unregister revokes them again (0x06000e39).
        [Test]
        public void DormantForeignConfigurationRowIsSetAsideAndRestoredUntouched()
        {
            IDictionary rows = Rows();
            object[] rowLayout = FSMManagerVerification.CaptureLayout(rows);
            string name = ProcessManager.GetDefaultName<SystemConfiguration>();
            object retained = rows.Contains(name) ? rows[name] : null;
            try
            {
                // Any retained configuration row is set aside so the dormant row is test-owned.
                rows.Remove(name);
                SystemRef reference = ProcessManager.GetSystemRef(name);
                SystemRef<SystemConfiguration> typed = ProcessManager.GetSystemRef<SystemConfiguration>(name);
                int notifications = 0;
                reference.OnSystemStartup += system => notifications++;
                reference.OnSystemShutdown += system => notifications++;
                typed.OnSystemStartup += system => notifications++;
                typed.OnSystemShutdown += system => notifications++;
                object row = rows[name];
                var typedCache = (IDictionary)row.GetType().GetField("SystemRefDictionary", Instance).GetValue(row);
                var cached = Entries(typedCache);
                List<object> keys = rows.Keys.Cast<object>().ToList();

                Assert.That(SystemConfigurationVerification.RunManaged(), Is.EqualTo(26));
                Assert.That(notifications, Is.Zero, "dormant row observers ran");
                Assert.That(rows[name], Is.SameAs(row));
                Assert.That(row.GetType().GetField("SystemRef", Instance).GetValue(row), Is.SameAs(reference));
                Assert.That(typedCache.Count, Is.EqualTo(cached.Count));
                foreach (DictionaryEntry entry in cached) Assert.That(typedCache[entry.Key], Is.SameAs(entry.Value), "typed reference replaced");
                Assert.That(reference.IsNull() && typed.IsNull(), Is.True, "dormant references acquired a system");
                Assert.That(rows.Keys.Cast<object>(), Is.EqualTo(keys), "registry row layout changed");
            }
            finally
            {
                rows.Remove(name);
                if (retained != null) rows[name] = retained;
                FSMManagerVerification.RestoreLayout(rows, rowLayout);
            }
        }

        // Regression guard rather than discriminating coverage of the refusal: the fixture removes the
        // configuration row it created, so a run from an absent row leaves the registry as it found it.
        [Test]
        public void RunFromAnAbsentRowLeavesNoConfigurationRow()
        {
            IDictionary rows = Rows();
            object[] rowLayout = FSMManagerVerification.CaptureLayout(rows);
            string name = ProcessManager.GetDefaultName<SystemConfiguration>();
            object retained = rows.Contains(name) ? rows[name] : null;
            try
            {
                rows.Remove(name);
                var before = Entries(rows);
                Assert.That(SystemConfigurationVerification.RunManaged(), Is.EqualTo(26));
                Assert.That(rows.Contains(name), Is.False, "the fixture left its configuration row behind");
                Assert.That(rows.Count, Is.EqualTo(before.Count));
                foreach (DictionaryEntry entry in before) Assert.That(rows[entry.Key], Is.SameAs(entry.Value));
            }
            finally
            {
                rows.Remove(name);
                if (retained != null) rows[name] = retained;
                FSMManagerVerification.RestoreLayout(rows, rowLayout);
            }
        }
    }
}
