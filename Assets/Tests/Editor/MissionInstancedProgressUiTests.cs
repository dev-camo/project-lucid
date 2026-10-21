using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The instanced-mission progress view: UIContainerInstancedMissionProgressParameters (ctor 0x63D620),
    // UIWidgetSDTSlot (0x6A2460..0x6A24B0) and UIContainerMissionInstancedProgress (SetupOrderedObjectives 0x63C740,
    // the AnimateProgressDelta state machine 0x63D160, OnDisable 0x63CEB0). Setup itself loads an icon through
    // Addressables and is not exercised here.
    public sealed class MissionInstancedProgressUiTests
    {
        private static UIContainerMissionInstancedProgress NewContainer(Owned owned, int slotCount, bool ordered = true)
        {
            var container = owned.Host("Lucid instanced progress", false).AddComponent<UIContainerMissionInstancedProgress>();
            var slots = new UIWidgetSDTSlot[slotCount];
            for (int i = 0; i < slotCount; i++) slots[i] = owned.Host("Lucid slot " + i, false).AddComponent<UIWidgetSDTSlot>();
            Set(container, "m_orderedObjectiveSlots", slots); // Reflection: authored serialized slots and toggle
            Set(container, "m_useOrderedObjectives", ordered);
            return container;
        }

        private static UIWidgetSDTSlot[] Slots(UIContainerMissionInstancedProgress container) =>
            Get<UIWidgetSDTSlot[]>(container, "m_orderedObjectiveSlots");

        private static bool[] Lit(UIContainerMissionInstancedProgress container) => Slots(container).Select(slot => slot.ActivateImage.Value).ToArray();

        private static UIContainerInstancedMissionProgressParameters Parameters(IReadOnlyList<int> previous, IReadOnlyList<int> completed) =>
            new UIContainerInstancedMissionProgressParameters(null, 0, 0, false, previous, completed);


        [Test]
        public void TheParametersKeepEachArgumentInItsOwnProperty()
        {
            Owned.Run(owned =>
            {
                var mission = owned.Track(ScriptableObject.CreateInstance<MissionDefinition>());
                var previous = new List<int> { 1 };
                var completed = new List<int> { 1, 2 };
                var parameters = new UIContainerInstancedMissionProgressParameters(mission, 7, 3, true, previous, completed);
                Assert.That(parameters.MissionDefinition, Is.SameAs(mission));
                Assert.That(parameters.Progress, Is.EqualTo(7));
                Assert.That(parameters.PreviousProgress, Is.EqualTo(3));
                Assert.That(parameters.AnimateProgressDelta, Is.True);
                Assert.That(parameters.PreviouslyCompletedObjectiveIndices, Is.SameAs(previous));
                Assert.That(parameters.CompletedObjectiveIndices, Is.SameAs(completed));
                Assert.That(default(UIContainerInstancedMissionProgressParameters).AnimateProgressDelta, Is.False);
            });
        }

        [Test]
        public void ASlotIsActivatedAndDeactivatedThroughItsBinding()
        {
            Owned.Run(owned =>
            {
                var slot = owned.Host("Lucid slot", false).AddComponent<UIWidgetSDTSlot>();
                Assert.That(slot.ActivateImage.Value, Is.False);
                slot.Activate();
                Assert.That(slot.ActivateImage.Value, Is.True);
                slot.Deactivate();
                Assert.That(slot.ActivateImage.Value, Is.False);
            });
        }

        // 0x6A24D0: the binding is written only when the requested state differs, so a first request for the state it
        // already holds never reaches the binding (whose first write always notifies).
        [Test]
        public void SettingTheSlotToItsCurrentStateWritesNothing()
        {
            Owned.Run(owned =>
            {
                var slot = owned.Host("Lucid slot", false).AddComponent<UIWidgetSDTSlot>();
                int notifications = 0;
                slot.ActivateImage.AddListener(() => notifications++);
                slot.SetSlotActive(false);
                Assert.That(notifications, Is.EqualTo(0));
                slot.SetSlotActive(true);
                Assert.That(notifications, Is.EqualTo(1));
                Assert.That(slot.ActivateImage.Value, Is.True);
                slot.SetSlotActive(false);
                Assert.That(slot.ActivateImage.Value, Is.False);
            });
        }

        // Counting up lights only the slots that were already complete; otherwise the newly completed light too. The
        // newly completed list is the completed minus the previously completed, sorted ascending.
        [Test]
        public void OrderedSlotsLightThePreviouslyCompletedAndTheNewlyCompletedOnlyWhenNotCountingUp()
        {
            Owned.Run(owned =>
            {
                UIContainerMissionInstancedProgress container = NewContainer(owned, 4);
                UIContainerInstancedMissionProgressParameters parameters = Parameters(new List<int> { 0 }, new List<int> { 3, 0, 2 });

                Call(container, "SetupOrderedObjectives", parameters, true);
                Assert.That(Lit(container), Is.EqualTo(new[] { true, false, false, false }));
                Assert.That(Get<IReadOnlyList<int>>(container, "m_newlyCompletedObjectives"), Is.EqualTo(new[] { 2, 3 }));

                Call(container, "SetupOrderedObjectives", parameters, false);
                Assert.That(Lit(container), Is.EqualTo(new[] { true, false, true, true }));
            });
        }

        [Test]
        public void MissingIndexListsAreTreatedAsEmpty()
        {
            Owned.Run(owned =>
            {
                UIContainerMissionInstancedProgress container = NewContainer(owned, 2);
                Call(container, "SetupOrderedObjectives", Parameters(null, new List<int> { 1 }), false);
                Assert.That(Lit(container), Is.EqualTo(new[] { false, true }), "no previous list: the completed one is all new");
                Assert.That(Get<IReadOnlyList<int>>(container, "m_newlyCompletedObjectives"), Is.EqualTo(new[] { 1 }));

                Call(container, "SetupOrderedObjectives", Parameters(new List<int> { 0 }, null), false);
                Assert.That(Lit(container), Is.EqualTo(new[] { true, false }), "no completed list: only the previous ones");
                Assert.That(Get<IReadOnlyList<int>>(container, "m_newlyCompletedObjectives"), Is.Empty);
            });
        }

        // 0x63D1C7..0x63D53E (MoveNext 0x63D160): initial delay, then while below the target (check at 0x63D48C) wait one
        // interval and tick, lighting the next newly completed slot; then flag the count-up and, after a delay, completion.
        [Test]
        public void TheProgressTicksUpOneStepPerIntervalLightingTheNewlyCompletedSlots()
        {
            Owned.Run(owned =>
            {
                UIContainerMissionInstancedProgress container = NewContainer(owned, 3);
                Set(container, "m_initialTickDelaySeconds", 1.5f); // Reflection: authored serialized timings
                Set(container, "m_animateTickIntervalSeconds", 0.25f);
                Set(container, "m_delayBeforeSetCompleteSeconds", 2.5f);
                Call(container, "SetupOrderedObjectives", Parameters(new List<int>(), new List<int> { 1, 2 }), true);
                container.Target.Value = 3;
                container.Progress.Value = 0;
                var animation = (IEnumerator)Call(container, "AnimateProgressDelta", 3);

                Assert.That(animation.MoveNext(), Is.True);
                Assert.That(OriginalInOutTestSupport.Seconds((WaitForSeconds)animation.Current), Is.EqualTo(1.5f), "the initial delay");
                Assert.That(container.Progress.Value, Is.EqualTo(0));

                Assert.That(animation.MoveNext(), Is.True);
                Assert.That(OriginalInOutTestSupport.Seconds((WaitForSeconds)animation.Current), Is.EqualTo(0.25f), "the tick interval is waited before the first tick");
                object interval = animation.Current;
                Assert.That(container.Progress.Value, Is.EqualTo(0));
                Assert.That(Lit(container), Is.EqualTo(new[] { false, false, false }));

                Assert.That(animation.MoveNext(), Is.True);
                Assert.That(animation.Current, Is.SameAs(interval), "one interval object is reused");
                Assert.That(container.Progress.Value, Is.EqualTo(1));
                Assert.That(Lit(container), Is.EqualTo(new[] { false, true, false }));

                Assert.That(animation.MoveNext(), Is.True);
                Assert.That(animation.Current, Is.SameAs(interval));
                Assert.That(container.Progress.Value, Is.EqualTo(2));
                Assert.That(Lit(container), Is.EqualTo(new[] { false, true, true }));
                Assert.That(container.CountUpComplete.Value, Is.False);

                Assert.That(animation.MoveNext(), Is.True);
                Assert.That(OriginalInOutTestSupport.Seconds((WaitForSeconds)animation.Current), Is.EqualTo(2.5f), "the delay before completion");
                Assert.That(container.Progress.Value, Is.EqualTo(3));
                Assert.That(container.CountUpComplete.Value, Is.True);
                Assert.That(container.IsComplete.Value, Is.False);

                Assert.That(animation.MoveNext(), Is.False);
                Assert.That(container.IsComplete.Value, Is.True, "the progress equals the target");
            });
        }

        [Test]
        public void TheCompletionFlagComparesTheProgressWithTheTargetWhenTheCountEnds()
        {
            Owned.Run(owned =>
            {
                UIContainerMissionInstancedProgress container = NewContainer(owned, 0, ordered: false);
                container.Target.Value = 5;
                container.Progress.Value = 1;
                var animation = (IEnumerator)Call(container, "AnimateProgressDelta", 3);
                while (animation.MoveNext()) { }
                Assert.That(container.Progress.Value, Is.EqualTo(3));
                Assert.That(container.IsComplete.Value, Is.False, "3 of 5");
            });
        }

        // The target is checked before every tick (0x63D48C): a count already at its target does not tick.
        [Test]
        public void ACountAlreadyAtItsTargetDoesNotTick()
        {
            Owned.Run(owned =>
            {
                UIContainerMissionInstancedProgress container = NewContainer(owned, 0, ordered: false);
                container.Progress.Value = 3;
                var animation = (IEnumerator)Call(container, "AnimateProgressDelta", 3);
                Assert.That(animation.MoveNext(), Is.True, "the initial delay");
                Assert.That(animation.MoveNext(), Is.True, "straight to the delay before completion");
                Assert.That(container.CountUpComplete.Value, Is.True);
                Assert.That(animation.MoveNext(), Is.False);
                Assert.That(container.Progress.Value, Is.EqualTo(3));
            });
        }

        // A slot index beyond the slot array is skipped rather than lit.
        [Test]
        public void ANewlyCompletedIndexBeyondTheSlotsIsSkipped()
        {
            Owned.Run(owned =>
            {
                UIContainerMissionInstancedProgress container = NewContainer(owned, 2);
                Call(container, "SetupOrderedObjectives", Parameters(new List<int>(), new List<int> { 5, 1 }), true);
                var animation = (IEnumerator)Call(container, "AnimateProgressDelta", 2);
                animation.MoveNext(); // initial delay
                animation.MoveNext(); // first interval
                animation.MoveNext(); // tick 1
                Assert.That(container.Progress.Value, Is.EqualTo(1));
                Assert.That(Lit(container), Is.EqualTo(new[] { false, true }), "index 1 lit by the first tick");
                animation.MoveNext(); // tick 2
                Assert.That(container.Progress.Value, Is.EqualTo(2));
                Assert.That(Lit(container), Is.EqualTo(new[] { false, true }), "index 5 has no slot");
            });
        }

        [Test]
        public void DisablingWithoutADefinitionDoesNothing()
        {
            Owned.Run(owned =>
            {
                UIContainerMissionInstancedProgress container = NewContainer(owned, 0, ordered: false);
                Assert.DoesNotThrow(() => Call(container, "OnDisable"));
            });
        }
    }
}
