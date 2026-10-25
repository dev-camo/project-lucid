using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    public sealed class SyntheticGraphNameOwnershipTests
    {
        private static Dictionary<int, string> Names => (Dictionary<int, string>)typeof(GraphNameLookup)
            .GetField("s_nameLookup", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

        [Test]
        public void StorageConditionProofLeavesNoConditionNamesAcrossRepeatedRuns()
        {
            int borrowed = GraphNameLookup.ConvertNameToId("Lucid-condition-preexisting-" + Guid.NewGuid().ToString("N"));
            try
            {
                var prior = new Dictionary<int, string>(Names);
                for (int run = 0; run < 2; run++)
                {
                    FSMStorageConditionVerification.Run();
                    AssertPreserved(prior);
                    AssertNoNewNames(prior, name => name.StartsWith("Lucid-condition-", StringComparison.Ordinal));
                }
            }
            finally { Names.Remove(borrowed); }
        }

        [Test]
        public void StorageConditionProofFailureLeavesNoConditionNames()
        {
            int borrowed = GraphNameLookup.ConvertNameToId("Lucid-condition-preexisting-" + Guid.NewGuid().ToString("N"));
            try
            {
                var prior = new Dictionary<int, string>(Names);
                var failure = new InvalidProgramException("storage names acquired");
                VerificationFixtureOwnership.FaultInjection = site =>
                {
                    if (site != FSMStorageConditionVerification.StorageNamesRegisteredSite) return;
                    Assert.That(Names.Any(row => !prior.ContainsKey(row.Key)
                        && row.Value.StartsWith("Lucid-condition-", StringComparison.Ordinal)), Is.True);
                    throw failure;
                };
                Assert.AreSame(failure, Assert.Throws<InvalidProgramException>(FSMStorageConditionVerification.Run));
                AssertPreserved(prior);
                AssertNoNewNames(prior, name => name.StartsWith("Lucid-condition-", StringComparison.Ordinal));
            }
            finally
            {
                VerificationFixtureOwnership.FaultInjection = null;
                Names.Remove(borrowed);
            }
        }

        [Test]
        public void NumericShortcutConstructorsReleaseTheirDerivedNames()
        {
            var prior = new Dictionary<int, string>(Names);
            for (int run = 0; run < 2; run++)
            {
                var fixture = new OriginalNumericFSMTests();
                fixture.OpenOwnedGraphNames();
                try { fixture.ShortcutConstructorsRetainOriginalDefaultIdentifierRoles(); }
                finally { fixture.ReleaseOwnedGraphNames(); }
                AssertPreserved(prior);
                AssertNoNewNames(prior, name => name.Contains("Lucid-numeric-"));
            }
        }

        [Test]
        public void NumericShortcutConstructorFailureReleasesTheirDerivedNames()
        {
            var prior = new Dictionary<int, string>(Names);
            var fixture = new OriginalNumericFSMTests();
            var failure = new InvalidProgramException("numeric names acquired");
            Assert.AreSame(failure, Assert.Throws<InvalidProgramException>(() =>
            {
                fixture.OpenOwnedGraphNames();
                try
                {
                    var machine = new FiniteStateMachine("Lucid-numeric-" + Guid.NewGuid().ToString("N"), skipAddToManager: true);
                    var key = new GraphStorageKey("Lucid-numeric-" + Guid.NewGuid().ToString("N"));
                    new FSMStateDiceRoll(machine, key, -2f, 3f);
                    new FSMTransitionCheckFloatInRange(machine, key, -1f, 1f);
                    new FSMTransitionCheckInt(machine, key, -7, FSMTransitionCheckInt.Comparison.Equal);
                    new FSMTransitionCheckFloat(machine, key, 2.5f, FSMTransitionCheckFloat.Comparison.Equal, 0.25f);
                    new FSMTransitionCheckFloat(machine, key, 2.5f, FSMTransitionCheckFloat.Comparison.GreaterThan, 99f);
                    Assert.That(machine.States.Count, Is.EqualTo(1));
                    Assert.That(machine.Transitions.Count, Is.EqualTo(4));
                    throw failure;
                }
                finally { fixture.ReleaseOwnedGraphNames(); }
            }));
            AssertPreserved(prior);
            AssertNoNewNames(prior, name => name.Contains("Lucid-numeric-"));
        }

        private static void AssertPreserved(Dictionary<int, string> prior)
        {
            foreach (var row in prior)
            {
                Assert.That(Names.TryGetValue(row.Key, out string name), Is.True, row.Value);
                Assert.That(name, Is.EqualTo(row.Value));
            }
        }

        private static void AssertNoNewNames(Dictionary<int, string> prior, Func<string, bool> owned)
        {
            // Fixed keys registered lazily by the original methods remain legitimate.
            Assert.That(Names.Where(row => !prior.ContainsKey(row.Key) && row.Value != null && owned(row.Value)), Is.Empty);
        }
    }
}
