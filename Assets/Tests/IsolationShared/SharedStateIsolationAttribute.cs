using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace ProjectLucid.Tests
{
    // Applied to a whole test assembly. Every test must leave the shared state
    // in SharedStateSnapshot exactly as it found it: a fixture may create and
    // remove its own rows, subscriptions and cache entries, but anything it
    // leaves behind, or any borrowed row, reference, observer, deferred work,
    // subscription or cache entry it replaced, fails the test with the changed
    // paths. The guard only reads; it never restores, so the leak stays visible.
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class SharedStateIsolationAttribute : Attribute, ITestAction
    {
        // Temporary and explicit: each entry names one exact test fullname and
        // why it cannot yet own the state it changes. Remove an entry with the
        // fix for that test; never add one to make a new test pass.
        internal static readonly IReadOnlyDictionary<string, string> TemporaryAllowlist = new Dictionary<string, string>
        {
        };

        private SharedStateSnapshot m_before;

        public ActionTargets Targets => ActionTargets.Test;

        public void BeforeTest(ITest test) => m_before = SharedStateSnapshot.Capture();

        public void AfterTest(ITest test)
        {
            SharedStateSnapshot before = m_before;
            m_before = null;
            if (before == null) return;
            string change = SharedStateSnapshot.Capture().DifferenceFrom(before);
            if (change == null || TemporaryAllowlist.ContainsKey(test.FullName)) return;
            Assert.Fail("Test left shared state changed: " + change);
        }
    }
}
