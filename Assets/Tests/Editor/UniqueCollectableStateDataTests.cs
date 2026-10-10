using System;
using HardlightProject;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    public sealed class UniqueCollectableStateDataTests
    {
        [Test]
        public void RegistrationAndCollectionKeepIndependentIdsAndCountOnlyOnce()
        {
            var state = new UniqueCollectableStateData();
            Assert.That(state.Collected, Is.Zero);
            Assert.That(state.IsCollected("missing"), Is.False);
            state.CollectId("missing", out bool changed);
            Assert.That(changed, Is.False);
            Assert.That(state.Collected, Is.Zero);

            state.TryAddId("ring", out bool collected);
            Assert.That(collected, Is.False);
            state.TryAddId("RING", out collected);
            Assert.That(collected, Is.False);
            Assert.That(state.Collected, Is.Zero);
            state.CollectId("ring", out changed);
            Assert.That(changed, Is.True);
            Assert.That(state.Collected, Is.EqualTo(1));
            Assert.That(state.IsCollected("ring"), Is.True);
            Assert.That(state.IsCollected("RING"), Is.False);
            state.TryAddId("ring", out collected);
            Assert.That(collected, Is.True);
            state.CollectId("ring", out changed);
            Assert.That(changed, Is.False);
            Assert.That(state.Collected, Is.EqualTo(1));
            state.CollectId("RING", out changed);
            Assert.That(changed, Is.True);
            Assert.That(state.Collected, Is.EqualTo(2));

            var other = new UniqueCollectableStateData();
            Assert.That(other.Collected, Is.Zero);
            other.CollectId("ring", out changed);
            Assert.That(changed, Is.False);
            Assert.That(state.Collected, Is.EqualTo(2));
        }

        [Test]
        public void ResetRetainsRegisteredIdsForRepeatedCollectionCycles()
        {
            var state = new UniqueCollectableStateData();
            state.Reset();
            foreach (string id in new[] { "a", "b", "c" })
            {
                state.TryAddId(id, out bool collected);
                Assert.That(collected, Is.False);
                state.CollectId(id, out bool changed);
                Assert.That(changed, Is.True);
            }
            Assert.That(state.Collected, Is.EqualTo(3));
            for (int cycle = 0; cycle < 2; cycle++)
            {
                state.Reset();
                Assert.That(state.Collected, Is.Zero);
                foreach (string id in new[] { "a", "b", "c" })
                {
                    Assert.That(state.IsCollected(id), Is.False);
                    // No registration between reset and collection: the original
                    // Reset retains IDs rather than clearing the dictionary.
                    state.CollectId(id, out bool changed);
                    Assert.That(changed, Is.True);
                }
                Assert.That(state.Collected, Is.EqualTo(3));
            }
        }

        [Test]
        public void NullKeyFaultsRetainDistinctOutputWriteOrderAndExistingState()
        {
            var state = new UniqueCollectableStateData();
            state.TryAddId("kept", out _);
            state.CollectId("kept", out _);
            bool output = true;
            Assert.Throws<ArgumentNullException>(() => state.TryAddId(null, out output));
            Assert.That(output, Is.True);
            Assert.Throws<ArgumentNullException>(() => state.CollectId(null, out output));
            Assert.That(output, Is.False);
            Assert.Throws<ArgumentNullException>(() => state.IsCollected(null));
            Assert.That(state.Collected, Is.EqualTo(1));
            Assert.That(state.IsCollected("kept"), Is.True);
            state.TryAddId("after-fault", out output);
            Assert.That(output, Is.False);
            state.CollectId("after-fault", out output);
            Assert.That(output, Is.True);
            Assert.That(state.Collected, Is.EqualTo(2));
        }
    }
}
