using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Editor
{
    // The FSM manager fixture must leave no fixture-owned registry row behind.
    public sealed class FSMManagerVerificationOwnershipTests
    {
        [Test]
        public void RepeatedRunsLeaveRegistryRowsAndSystemsUnchanged()
        {
            IDictionary rows = (IDictionary)typeof(ProcessManager)
                .GetField("s_systemDictionary", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            Dictionary<string, bool> before = rows.Keys.Cast<string>().ToDictionary(name => name, name => ProcessManager.IsSystemNull(name));
            FSMManagerVerification.Run();
            FSMManagerVerification.Run();
            Assert.That(rows.Keys.Cast<string>(), Is.EquivalentTo(before.Keys));
            foreach (KeyValuePair<string, bool> row in before)
                Assert.That(ProcessManager.IsSystemNull(row.Key), Is.EqualTo(row.Value), row.Key);
        }
    }
}
