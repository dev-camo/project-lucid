using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerCollectableCounter (Game.Runtime 02000892, x86-64 0x624890..0x625170, iterator 02000893) as a
    // real component on an owned host. Its field initialisers take the ProgressionManager and SaveManager
    // references, so those registry rows are leased first. The counter is added active only where a case needs
    // StartCoroutine, which in Edit Mode runs the first iterator step at once. Setup and the save-backed update are
    // not covered: they read the collectable definition's addressable icon, the ProgressionManager and the open
    // save. Refresh is covered for the gameplay path only.
    public sealed class UIContainerCollectableCounterTests
    {
        private static void WithCounter(bool active, Action<UIContainerCollectableCounter, List<int>, List<int>> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<ProgressionManager>();
            owned.LeaseRow<SaveManager>();
            var counter = owned.Host("Lucid collectable counter", active).AddComponent<UIContainerCollectableCounter>();
            var positive = new List<int>();
            var negative = new List<int>();
            // Serialized, protected authored events.
            var onPositive = new UnityEvent<int>();
            onPositive.AddListener(positive.Add);
            var onNegative = new UnityEvent<int>();
            onNegative.AddListener(negative.Add);
            Set(counter, "m_onPositiveChange", onPositive);
            Set(counter, "m_onNegativeChange", onNegative);
            body(counter, positive, negative);
        });

        private static CollectableState State(int total, int collected)
        {
            var state = new CollectableState();
            state.AddCollectable(total);
            state.AdjustCollected(collected, true);
            return state;
        }

        private static CollectableChangeMetadata From(CollectableSource source) => new CollectableChangeMetadata { Source = source };

        // dump.cs TypeDefIndex 2194/2193 and the DummyDll rows (fields 0x040021d5..0x040021e3).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerCollectableCounter), 0x00100001,
                new (string, int)[]
                {
                    ("m_type", 0x0001),
                    ("m_formatStringOverride", 0x0001),
                    ("m_onPositiveChange", 0x0004),
                    ("m_onNegativeChange", 0x0004),
                    ("m_ringCountRate", 0x0001),
                    ("TotalAmount", 0x0026),
                    ("CollectedAmount", 0x0026),
                    ("Icon", 0x0026),
                    ("HasValues", 0x0026),
                    ("DisplayFormattedString", 0x0026),
                    ("m_progressionManagerRef", 0x0021),
                    ("m_saveManagerRef", 0x0021),
                    ("m_parameters", 0x0001),
                    ("m_collectedTarget", 0x0001),
                    ("m_collectedCoroutine", 0x0001),
                },
                new (string, int)[]
                {
                    ("get_Type", 0x0886),
                    ("Setup", 0x00c6),
                    ("UpdateFromGameplay", 0x0081),
                    ("UpdateFromSave", 0x0081),
                    ("LerpToCollectedTarget", 0x0081),
                    ("OnCollectableStateUpdate", 0x01c6),
                    ("Refresh", 0x0086),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerCollectableCounter), 2, 1);
            Assert.That(typeof(UIContainerCollectableCounter).BaseType, Is.EqualTo(typeof(UIContainer)));
            var metadata = typeof(UIContainerCollectableCounter).GetMethod("OnCollectableStateUpdate").GetParameters()[1];
            Assert.That(metadata.IsOptional, Is.True, "metadata is an optional parameter (default(T))");
            Assert.That(AttributeTypes(Field(typeof(UIContainerCollectableCounter), "m_formatStringOverride")),
                Is.EqualTo(new[] { typeof(SerializeField), typeof(Hardlight.Utils.HashEnumAttribute), typeof(TooltipAttribute) }));
        }

        // The constructor 0x624d70 sets the format override to Strings.NONE (0xb6fa1284) and the ring rate to
        // 0x3c23d70a, creates the five bindables and takes the two manager references.
        [Test]
        public void ConstructionStartsWithNoFormatOverrideAndTheRingRate()
        {
            WithCounter(false, (counter, positive, negative) =>
            {
                Assert.That(Get<Strings>(counter, "m_formatStringOverride"), Is.EqualTo(Strings.NONE));
                Assert.That(Get<float>(counter, "m_ringCountRate"), Is.EqualTo(0.01f));
                Assert.That(counter.TotalAmount.Value, Is.Zero);
                Assert.That(counter.CollectedAmount.Value, Is.Zero);
                Assert.That(counter.HasValues.Value, Is.False);
                Assert.That(counter.Icon.Value, Is.Null);
                Assert.That(counter.DisplayFormattedString.Value, Is.Null);
                Assert.That(Get<object>(counter, "m_progressionManagerRef"), Is.Not.Null);
                Assert.That(Get<object>(counter, "m_saveManagerRef"), Is.Not.Null);
            });
        }

        // get_Type 0x624890 returns the authored type field.
        [Test]
        public void TypeIsTheAuthoredCollectableType()
        {
            WithCounter(false, (counter, positive, negative) =>
            {
                Set(counter, "m_type", CollectableType.BlueCoin);
                Assert.That(counter.Type, Is.EqualTo(CollectableType.BlueCoin));
                Set(counter, "m_type", CollectableType.Orb);
                Assert.That(counter.Type, Is.EqualTo(CollectableType.Orb));
            });
        }

        // OnCollectableStateUpdate 0x624be0 reads the old count, writes the total (state +0x10) and HasValues, and with
        // no running count sets the collected amount (state +0x14) at once. A positive difference raises the positive
        // event with it (jle at 0x624d27, Invoke at 0x624d54).
        [Test]
        public void AnIncreaseIsShownAtOnceAndReportedAsPositive()
        {
            WithCounter(false, (counter, positive, negative) =>
            {
                counter.OnCollectableStateUpdate(State(10, 4), default);

                Assert.That(counter.TotalAmount.Value, Is.EqualTo(10));
                Assert.That(counter.CollectedAmount.Value, Is.EqualTo(4));
                Assert.That(counter.HasValues.Value, Is.True);
                Assert.That(positive, Is.EqualTo(new[] { 4 }));
                Assert.That(negative, Is.Empty);
            });
        }

        // A decrease raises the negative event with the signed difference (the second branch at 0x624d34..0x624d54).
        [Test]
        public void ADecreaseIsReportedAsNegativeWithItsSign()
        {
            WithCounter(false, (counter, positive, negative) =>
            {
                counter.OnCollectableStateUpdate(State(10, 4), default);
                positive.Clear();

                counter.OnCollectableStateUpdate(State(10, 1), default);

                Assert.That(counter.CollectedAmount.Value, Is.EqualTo(1));
                Assert.That(negative, Is.EqualTo(new[] { -3 }));
                Assert.That(positive, Is.Empty);
            });
        }

        // An unchanged count raises neither event (je at 0x624d37), though the total is still written.
        [Test]
        public void AnUnchangedCountRaisesNoEvent()
        {
            WithCounter(false, (counter, positive, negative) =>
            {
                counter.OnCollectableStateUpdate(State(10, 4), default);
                positive.Clear();

                counter.OnCollectableStateUpdate(State(12, 4), default);

                Assert.That(counter.TotalAmount.Value, Is.EqualTo(12));
                Assert.That(positive, Is.Empty);
                Assert.That(negative, Is.Empty);
            });
        }

        // Both event fields are tested for null before Invoke (0x624d2d, 0x624d40).
        [Test]
        public void MissingEventsAreSkipped()
        {
            WithCounter(false, (counter, positive, negative) =>
            {
                Set(counter, "m_onPositiveChange", null);
                Set(counter, "m_onNegativeChange", null);

                Assert.DoesNotThrow(() => counter.OnCollectableStateUpdate(State(10, 4), default));
                Assert.DoesNotThrow(() => counter.OnCollectableStateUpdate(State(10, 1), default));
                Assert.That(counter.CollectedAmount.Value, Is.EqualTo(1));
            });
        }

        // Only a MissionStart update (0x81d33f74) starts the counting coroutine (StartCoroutine 0x624cc1): the
        // collected amount restarts from zero instead of jumping, and the running routine is kept for later updates.
        [Test]
        public void AMissionStartCountsUpInsteadOfJumping()
        {
            WithCounter(true, (counter, positive, negative) =>
            {
                Set(counter, "m_ringCountRate", 1000f); // authored; the first step then cannot add a ring
                counter.OnCollectableStateUpdate(State(10, 4), From(CollectableSource.MissionStart));

                Assert.That(counter.TotalAmount.Value, Is.EqualTo(10));
                Assert.That(counter.CollectedAmount.Value, Is.Zero, "the count starts again from zero");
                Assert.That(Get<Coroutine>(counter, "m_collectedCoroutine"), Is.Not.Null);
                Assert.That(Get<int>(counter, "m_collectedTarget"), Is.EqualTo(4));
                Assert.That(positive, Is.Empty, "nothing has been added yet");
            });
        }

        // On a MissionStart update StartCoroutine has already run the first iterator step, which zeroed the amount, when
        // the change is taken: the difference is the zeroed amount minus the previous one (get_Value 0x624d1a, then
        // sub/jle/Invoke 0x624d22..0x624d54), so a count of 3 restarted towards 4 reports -3, not the target's +1.
        [Test]
        public void AMissionStartReportsTheDropToZeroAgainstThePreviousCount()
        {
            WithCounter(true, (counter, positive, negative) =>
            {
                Set(counter, "m_ringCountRate", 1000f); // authored; the first step then cannot add a ring
                counter.OnCollectableStateUpdate(State(10, 3), default);
                positive.Clear();

                counter.OnCollectableStateUpdate(State(10, 4), From(CollectableSource.MissionStart));

                Assert.That(negative, Is.EqualTo(new[] { -3 }));
                Assert.That(positive, Is.Empty);
                Assert.That(counter.CollectedAmount.Value, Is.Zero);
            });
        }

        // Refresh 0x616fd0 branches on the parameters' InGameplay flag (+0xa1, set by the state constructor 0x6251ee) and
        // then on CollectableExists (+0xa0): a missing collectable clears HasValues (0x61700a); an existing one shows its state.
        [Test]
        public void RefreshInGameplayClearsOrShowsTheState()
        {
            WithCounter(false, (counter, positive, negative) =>
            {
                Set(counter, "m_parameters", new UIContainerCollectableCounterParameters(State(5, 2), null, false));
                counter.HasValues.Value = true;

                counter.Refresh();

                Assert.That(counter.HasValues.Value, Is.False);
                Assert.That(counter.TotalAmount.Value, Is.Zero);

                Set(counter, "m_parameters", new UIContainerCollectableCounterParameters(State(5, 2), null, true));
                counter.Refresh();

                Assert.That(counter.TotalAmount.Value, Is.EqualTo(5));
                Assert.That(counter.CollectedAmount.Value, Is.EqualTo(2));
                Assert.That(counter.HasValues.Value, Is.True);
            });
        }

        // While a count is running, a non-start update only retargets it (0x624ce8..0x624cf2): the displayed amount
        // does not move.
        [Test]
        public void ARunningCountIsRetargetedNotOverwritten()
        {
            WithCounter(true, (counter, positive, negative) =>
            {
                Set(counter, "m_ringCountRate", 1000f);
                counter.OnCollectableStateUpdate(State(10, 4), From(CollectableSource.MissionStart));

                counter.OnCollectableStateUpdate(State(10, 7), From(CollectableSource.Level));

                Assert.That(counter.CollectedAmount.Value, Is.Zero);
                Assert.That(Get<int>(counter, "m_collectedTarget"), Is.EqualTo(7));
                Assert.That(positive, Is.Empty);
            });
        }

        // MoveNext 0x624fb0: the amount is zeroed and the target and timer set on the first step; each later step
        // subtracts the frame time and, once the timer is no longer positive, adds one and refills the timer with
        // the ring rate. A zero rate therefore adds one per step, the target may move mid-count, and reaching it ends
        // the routine with the amount set to the target and the stored routine cleared.
        [Test]
        public void TheCountAddsOnePerStepAndEndsAtTheTarget()
        {
            WithCounter(false, (counter, positive, negative) =>
            {
                Set(counter, "m_ringCountRate", 0f);
                counter.CollectedAmount.Value = 9;
                var routine = (IEnumerator)Call(counter, "LerpToCollectedTarget", 3);

                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(counter.CollectedAmount.Value, Is.EqualTo(1), "zeroed, then one added");
                Assert.That(Get<int>(counter, "m_collectedTarget"), Is.EqualTo(3));
                Assert.That(routine.Current, Is.Null);
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(counter.CollectedAmount.Value, Is.EqualTo(2));

                Set(counter, "m_collectedTarget", 2); // retargeted while counting
                Assert.That(routine.MoveNext(), Is.False);
                Assert.That(counter.CollectedAmount.Value, Is.EqualTo(2));
                Assert.That(Get<Coroutine>(counter, "m_collectedCoroutine"), Is.Null);
            });
        }

        // With a ring rate far above the frame time the timer stays positive, so a step waits (state 1) without adding.
        [Test]
        public void ASlowRingRateWaitsWithoutAddingAnything()
        {
            WithCounter(false, (counter, positive, negative) =>
            {
                Set(counter, "m_ringCountRate", 1000f);
                var routine = (IEnumerator)Call(counter, "LerpToCollectedTarget", 5);

                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(counter.CollectedAmount.Value, Is.Zero);
            });
        }

        // A target of zero ends the routine at the first step: the amount is zeroed and then set to the target.
        [Test]
        public void AZeroTargetEndsAtOnce()
        {
            WithCounter(false, (counter, positive, negative) =>
            {
                counter.CollectedAmount.Value = 5;
                var routine = (IEnumerator)Call(counter, "LerpToCollectedTarget", 0);

                Assert.That(routine.MoveNext(), Is.False);
                Assert.That(counter.CollectedAmount.Value, Is.Zero);
            });
        }
    }
}
