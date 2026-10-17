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
    // configuration it does not own before any registry access.
    public sealed class SystemConfigurationVerificationOwnershipTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static IDictionary Rows() => (IDictionary)typeof(ProcessManager)
            .GetField("s_systemDictionary", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

        private static IDictionary ActionRows() => (IDictionary)typeof(ProcessManager)
            .GetField("s_systemActionLookup", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

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

        [Test]
        public void ColdVerificationPassesAndLeavesNoLiveConfiguration()
        {
            Assert.That(ProcessManager.IsSystemNull<SystemConfiguration>(), Is.True, "this case owns the configuration row only from a cold Editor state");
            Assert.That(SystemConfigurationVerification.RunManaged(), Is.EqualTo(27));
            Assert.That(ProcessManager.IsSystemNull<SystemConfiguration>(), Is.True, "the fixture unregisters its own configuration");
        }
    }
}
