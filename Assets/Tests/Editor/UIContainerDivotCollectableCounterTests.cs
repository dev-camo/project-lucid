using System;
using System.Linq;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerDivotCollectableCounter (Game.Runtime 0x020008b3, x86-64 0x62F700..0x62F7A0) as a real component over three real
    // UIWidgetSDTSlot components. Like its base (UIContainerCollectableCounter), its field initialisers take the ProgressionManager
    // and SaveManager references, so those registry rows are leased first. The slot array is serialized authored data. The counter
    // is added active only where a case needs StartCoroutine.
    public sealed class UIContainerDivotCollectableCounterTests
    {
        private static void Run(bool active, Action<UIContainerDivotCollectableCounter, UIWidgetSDTSlot[]> body, int slotCount = 3) =>
            Owned.Run(owned =>
            {
                owned.LeaseRow<ProgressionManager>();
                owned.LeaseRow<SaveManager>();
                var counter = owned.Host("Lucid divot counter", active).AddComponent<UIContainerDivotCollectableCounter>();
                UIWidgetSDTSlot[] slots = Enumerable.Range(0, slotCount)
                    .Select(index => owned.Host("Lucid divot slot " + index, false).AddComponent<UIWidgetSDTSlot>()).ToArray();
                Set(counter, "m_slots", slots); // Reflection: serialized authored array
                body(counter, slots);
            });

        private static CollectableState State(int total, int collected)
        {
            var state = new CollectableState();
            state.AddCollectable(total);
            state.AdjustCollected(collected, true);
            return state;
        }

        private static bool[] Filled(UIWidgetSDTSlot[] slots) => slots.Select(slot => slot.ActivateImage.Value).ToArray();

        // dump.cs TypeDefIndex 2226 and the DummyDll rows (field 0x04002294, methods 0x0600323f, 0x06003240).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerDivotCollectableCounter), 0x00100101,
                new (string, int)[] { ("m_slots", 0x0001) },
                new (string, int)[] { ("OnCollectableStateUpdate", 0x00c6), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIContainerDivotCollectableCounter), 1, 2);
            Assert.That(typeof(UIContainerDivotCollectableCounter).BaseType, Is.EqualTo(typeof(UIContainerCollectableCounter)));
            Assert.That(AttributeTypes(typeof(UIContainerDivotCollectableCounter).GetField("m_slots", Declared)), Is.EqualTo(new[] { typeof(SerializeField) }));
        }

        // OnCollectableStateUpdate 0x62F700 returns without calling the base (0x62F71E, jge 0x62F784) once the collected amount
        // (state +0x14) reaches the slot count; below it, slot i is activated while i < slots and i < collected (0x62F73F..0x62F765) and
        // the base update runs (jmp 0x624BE0), which publishes the total and the amount and sets HasValues. Truth table, three slots.
        [TestCase(0, 0, true, TestName = "NoCollectedItemFillsNoSlotButTheCounterUpdates")]
        [TestCase(1, 1, true, TestName = "OneCollectedItemFillsOneSlot")]
        [TestCase(2, 2, true, TestName = "TwoCollectedItemsFillTwoSlots")]
        [TestCase(3, 0, false, TestName = "ACollectedCountEqualToTheSlotCountSkipsTheWholeUpdate")]
        [TestCase(4, 0, false, TestName = "ACollectedCountAboveTheSlotCountSkipsTheWholeUpdate")]
        public void TheSlotsFillUpToTheCollectedCountOnlyBelowTheSlotCount(int collected, int expectedFilled, bool updatesCounter)
        {
            Run(false, (counter, slots) =>
            {
                counter.OnCollectableStateUpdate(State(10, collected), default);

                Assert.That(Filled(slots), Is.EqualTo(Enumerable.Range(0, 3).Select(index => index < expectedFilled).ToArray()));
                Assert.That(counter.HasValues.Value, Is.EqualTo(updatesCounter));
                Assert.That(counter.TotalAmount.Value, Is.EqualTo(updatesCounter ? 10 : 0));
                Assert.That(counter.CollectedAmount.Value, Is.EqualTo(updatesCounter ? collected : 0));
            });
        }

        // The loop only activates (0x62F74D calls Activate, never Deactivate): a slot filled earlier stays filled when fewer items are
        // collected afterwards.
        [Test]
        public void AFilledSlotIsNeverEmptiedByALaterUpdate()
        {
            Run(false, (counter, slots) =>
            {
                slots[2].Activate();

                counter.OnCollectableStateUpdate(State(10, 1), default);

                Assert.That(Filled(slots), Is.EqualTo(new[] { true, false, true }));
            });
        }

        // With no slots the collected amount (0) is not below the slot count (0), so nothing happens at all.
        [Test]
        public void WithoutSlotsTheCounterIsNotUpdated()
        {
            Run(false, (counter, slots) =>
            {
                counter.OnCollectableStateUpdate(State(10, 0), default);

                Assert.That(counter.HasValues.Value, Is.False);
                Assert.That(counter.TotalAmount.Value, Is.Zero);
            }, slotCount: 0);
        }

        // The metadata argument (edx, kept in ebx across the loop) is handed to the base: a MissionStart update starts the base's
        // counting coroutine, so the amount restarts from zero (as in UIContainerCollectableCounterTests) while the slots fill at once.
        [Test]
        public void TheMetadataReachesTheBaseUpdate()
        {
            Run(true, (counter, slots) =>
            {
                Set(counter, "m_ringCountRate", 1000f); // Reflection: serialized rate; the first step then cannot add a ring

                counter.OnCollectableStateUpdate(State(10, 2), new CollectableChangeMetadata { Source = CollectableSource.MissionStart });

                Assert.That(Filled(slots), Is.EqualTo(new[] { true, true, false }));
                Assert.That(counter.TotalAmount.Value, Is.EqualTo(10));
                Assert.That(counter.CollectedAmount.Value, Is.Zero, "the base counts up from zero");
            });
        }
    }
}
