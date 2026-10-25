using System.Collections.Generic;
using Hardlight;
using Hardlight.Pooling;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    public sealed class FSMStorageHistoryClearTests
    {
        [Test]
        public void ClearStateHistoryDrainsAfterTheMaximumIsZeroed() => VerifyClear(0, false);

        [Test]
        public void ClearStateHistoryDrainsAfterTheMaximumIsNegative() => VerifyClear(-1, false);

        [Test]
        public void ClearStateHistoryDrainsAfterTheMaximumIsRemoved() => VerifyClear(null, false);

        [Test]
        public void ClearDrainsBeforeDiscardingEntriesAfterTheMaximumIsZeroed() => VerifyClear(0, true);

        [Test]
        public void ClearStateHistoryDrainsWithAPositiveMaximum() => VerifyClear(2, false);

        // Original 0x060004be, x86-64 0x1ACD6D0: fetch existing history without consulting the maximum,
        // then dequeue each action and Destroy(true) at 0x1ACD84B. Clear drains before discarding storage.
        private static void VerifyClear(int? maximum, bool clearStorage)
        {
            using (new VerificationFixtureOwnership.ActionPoolLease())
            {
                var storage = new FSMStorage(2);
                int baseline = ObjectPool<FSMStateChangeAction>.UsedObjectCount;
                var queue = new Queue<FSMStateChangeAction>();
                queue.Enqueue(FSMStateChangeAction.Create(FSMActionReason.Forced));
                Assert.That(ObjectPool<FSMStateChangeAction>.UsedObjectCount, Is.EqualTo(baseline + 1));

                var historyKey = new GraphStorageKey("stateHistory");
                var maximumKey = new GraphStorageKey("stateHistoryMaxCount");
                storage.SetValue(historyKey, new Dictionary<int, Queue<FSMStateChangeAction>> { { 42, queue } });
                if (maximum.HasValue)
                    storage.SetValue<int>(maximumKey, maximum.Value);
                else
                    storage.RemoveValue<int>(maximumKey);
                int storedKeyCount = storage.GetCollection().Count;

                if (clearStorage)
                    storage.Clear();
                else
                    storage.ClearStateHistory();

                Assert.That(queue.Count, Is.Zero);
                Assert.That(ObjectPool<FSMStateChangeAction>.UsedObjectCount, Is.EqualTo(baseline));
                if (clearStorage)
                    Assert.That(storage.GetCollection().Count, Is.Zero);
                else
                {
                    Assert.That(storage.GetCollection().Count, Is.EqualTo(storedKeyCount));
                    Assert.That(storage.GetCollection().ContainsKey(historyKey), Is.True);
                    Assert.That(storage.GetCollection().ContainsKey(maximumKey), Is.EqualTo(maximum.HasValue));
                    Assert.That(storage.StateHistory[42], Is.SameAs(queue));
                }
            }
        }
    }
}
