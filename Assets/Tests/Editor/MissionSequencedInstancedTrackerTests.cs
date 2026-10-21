using System;
using System.Collections.Generic;
using System.Linq;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.MissionTrackerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // MissionSequencedInstancedTracker (Game.Runtime 02000718: Initialise 0x5C0EF0, StartMission 0x5C0FA0, StatusChanged
    // 0x5C13E0) over real objectives, a real mission state and save record and the real managers a tracker reaches.
    // Which objectives the tracker activated is read from the objectives' own activation events. OnDrawGizmosSelected
    // (0x5C1820) draws with Gizmos outside a gizmo callback, so it is not run.
    public sealed class MissionSequencedInstancedTrackerTests
    {
        private sealed class Rig
        {
            internal Mission Mission;
            internal MissionSequencedInstancedTracker Tracker;
            internal List<MissionObjective> Objectives;
            internal List<CountingEvent> Activated;
            internal List<CountingEvent> Reset;
            internal int StatusCallbacks;

            internal int[] ActivationCounts => Activated.Select(activated => activated.Count).ToArray();
        }

        // The tracker and its objectives are made and authored, handed to beforeInitialise, then initialised.
        private static Rig Build(World world, int count, int? limit = null, Action<Rig> beforeInitialise = null, Mission mission = null)
        {
            var rig = new Rig { Mission = mission ?? world.NewMission(count, count) };
            rig.Tracker = world.NewTracker<MissionSequencedInstancedTracker>();
            rig.Objectives = Enumerable.Range(0, count).Select(_ => world.NewObjective<MissionObjective>(rig.Tracker.transform)).ToList();
            rig.Activated = rig.Objectives.Select(objective => Author(objective, "m_onActivated")).ToList();
            rig.Reset = rig.Objectives.Select(objective => Author(objective, "m_onReset")).ToList();
            Set(rig.Tracker, "m_missionObjectives", new List<MissionObjective>(rig.Objectives)); // Reflection: authored list (the editor Reset finds none on inactive hosts)
            if (limit.HasValue)
                Set(rig.Tracker, "m_activeObjectiveLimit", limit.Value); // Reflection: authored limit
            Author(rig.Tracker, "m_onAnyObjectiveCompleted");
            Author(rig.Tracker, "m_onLastObjective");
            rig.Tracker.OnStatusChanged = _ => rig.StatusCallbacks++;
            beforeInitialise?.Invoke(rig);
            rig.Tracker.Initialise(rig.Mission.State, null);
            return rig;
        }

        private static void Run(int count, int? limit, Action<Rig> body, Action<Rig> beforeInitialise = null) =>
            WithWorld(world => body(Build(world, count, limit, beforeInitialise)));

        // Constructor 0x5C1B70 stores 1 in the limit before the sets are created.
        [Test]
        public void TheDefaultLimitActivatesOnlyTheFirstObjectiveOnStart()
        {
            Run(3, null, rig =>
            {
                Assert.That(Get<int>(rig.Tracker, "m_activeObjectiveLimit"), Is.EqualTo(1));
                rig.Tracker.StartMission(false);
                Assert.That(rig.ActivationCounts, Is.EqualTo(new[] { 1, 0, 0 }));
                Assert.That(rig.Objectives.Select(objective => objective.IsActive).ToArray(), Is.EqualTo(new[] { true, false, false }));
            });
        }

        // 0x5C1246: an objective is activated while the number activated so far is below the limit (jl), else it waits.
        [Test]
        public void StartActivatesTheFirstLimitObjectivesInOrderAndTheRestWait()
        {
            Run(4, 2, rig =>
            {
                rig.Tracker.StartMission(false);
                Assert.That(rig.ActivationCounts, Is.EqualTo(new[] { 1, 1, 0, 0 }));
            });
            Run(2, 5, rig =>
            {
                rig.Tracker.StartMission(false);
                Assert.That(rig.ActivationCounts, Is.EqualTo(new[] { 1, 1 }), "a limit above the count activates all");
            });
        }

        // 0x5C11CB..0x5C1241: every objective is reset without a status report (completion cleared, reset event raised)
        // before it is activated; the base start-up also marks the mission started.
        [Test]
        public void StartResetsEveryObjectiveWithoutReportingStatusBeforeActivating()
        {
            Run(2, 1, rig =>
            {
                rig.Objectives[1].Action_ObjectiveComplete();
                Assert.That(rig.Objectives[1].IsComplete, Is.True);
                rig.StatusCallbacks = 0;

                rig.Tracker.StartMission(false);
                Assert.That(rig.Objectives[1].IsComplete, Is.False);
                Assert.That(rig.Reset.Select(reset => reset.Count).ToArray(), Is.EqualTo(new[] { 1, 1 }));
                Assert.That(rig.StatusCallbacks, Is.EqualTo(0));
                Assert.That(rig.Tracker.ObjectivesComplete, Is.EqualTo(0));

                rig.Objectives[0].Action_ObjectiveComplete();
                Assert.That(rig.Tracker.ObjectivesComplete, Is.EqualTo(1), "the mission is started: the base counts the completion");
            });
        }

        // 0x5C145B..0x5C1473: neither started nor persisting means the whole override, base included, is skipped.
        [Test]
        public void AStatusChangeBeforeStartIsIgnoredEntirely()
        {
            Run(3, 1, rig =>
            {
                rig.Objectives[0].Action_ObjectiveComplete();
                Assert.That(rig.Tracker.ObjectivesComplete, Is.EqualTo(0));
                Assert.That(rig.StatusCallbacks, Is.EqualTo(0));
                Assert.That(rig.ActivationCounts, Is.EqualTo(new[] { 0, 0, 0 }));
            });
        }

        // 0x5C13E0..0x5C17DD: a completion removes the objective from both sets and the loop then activates the first
        // not-yet-active, incomplete objective while some wait and fewer than the limit are active.
        [Test]
        public void CompletingTheActiveObjectiveActivatesTheNextWaitingOne()
        {
            Run(3, 1, rig =>
            {
                rig.Tracker.StartMission(false);
                rig.Objectives[0].Action_ObjectiveComplete();
                Assert.That(rig.ActivationCounts, Is.EqualTo(new[] { 1, 1, 0 }));
                Assert.That(rig.Tracker.ObjectivesComplete, Is.EqualTo(1), "the base counted the completion");
                Assert.That(rig.StatusCallbacks, Is.EqualTo(1));

                rig.Objectives[1].Action_ObjectiveComplete();
                Assert.That(rig.ActivationCounts, Is.EqualTo(new[] { 1, 1, 1 }));
                Assert.That(rig.Tracker.ObjectivesComplete, Is.EqualTo(2));
            });
        }

        // With two active of four, one completion frees one slot: the first waiting objective takes it, then the count
        // reaches the limit (0x5C1689, jge) and the loop stops before the last objective.
        [Test]
        public void EachCompletionFreesOneSlotForTheFirstWaitingObjective()
        {
            Run(4, 2, rig =>
            {
                rig.Tracker.StartMission(false);
                rig.Objectives[1].Action_ObjectiveComplete();
                Assert.That(rig.ActivationCounts, Is.EqualTo(new[] { 1, 1, 1, 0 }));

                rig.Objectives[0].Action_ObjectiveComplete();
                Assert.That(rig.ActivationCounts, Is.EqualTo(new[] { 1, 1, 1, 1 }));
            });
        }

        // 0x5C1696 skips an active objective and 0x5C16A5 a complete one. With limit 1, a still-waiting objective that
        // completes is complete but not active, so when the active one completes the loop passes it over.
        [Test]
        public void ACompletedWaitingObjectiveIsSkippedWhenActivating()
        {
            Run(3, 1, rig =>
            {
                rig.Tracker.StartMission(false);
                rig.Objectives[1].Action_ObjectiveComplete();
                Assert.That(rig.ActivationCounts, Is.EqualTo(new[] { 1, 0, 0 }), "the limit is already reached");

                rig.Objectives[0].Action_ObjectiveComplete();
                Assert.That(rig.ActivationCounts, Is.EqualTo(new[] { 1, 0, 1 }));
            });
        }

        // The base StatusChanged (0x5C17DD) runs after the activation loop, so its callback already sees the next
        // objective active.
        [Test]
        public void TheBaseStatusChangeRunsAfterTheNextObjectiveIsActivated()
        {
            Run(2, 1, rig =>
            {
                bool seen = false;
                rig.Tracker.OnStatusChanged = _ => seen = rig.Objectives[1].IsActive;
                rig.Tracker.StartMission(false);
                rig.Objectives[0].Action_ObjectiveComplete();
                Assert.That(seen, Is.True);
            });
        }

        // 0x5C1496..0x5C16F3: an objective that reports itself neither complete nor active (a reset) goes back to
        // the waiting set and, being first in order, takes the free slot again.
        [Test]
        public void AResetObjectiveWaitsAndIsActivatedAgainInOrder()
        {
            Run(2, 1, rig =>
            {
                rig.Tracker.StartMission(false);
                rig.Objectives[0].OnObjectiveReset();
                Assert.That(rig.ActivationCounts, Is.EqualTo(new[] { 2, 0 }));
                Assert.That(rig.Objectives[0].IsActive, Is.True);
            });
        }

        // The gate at 0x5C145B..0x5C146D passes when either the started flag or the persistence flag is set. Persisting
        // trackers handle a report before the mission starts: a reset report files its objective as waiting and the
        // loop then activates the first objective, which was never filed.
        [Test]
        public void APersistingTrackerHandlesReportsBeforeTheMissionStarts()
        {
            foreach ((bool persist, int[] expected) in new[] { (true, new[] { 1, 0 }), (false, new[] { 0, 0 }) })
            {
                Run(2, 1, rig =>
                {
                    rig.Objectives[1].OnObjectiveReset();
                    Assert.That(rig.ActivationCounts, Is.EqualTo(expected), "persisting " + persist);
                }, rig => Set(rig.Tracker, "m_persistObjectiveCompletion", persist)); // Reflection: authored toggle
            }
        }

        // Initialise 0x5C0EF0: a mission that is not complete starts with both sets empty (nothing waits, so the
        // third objective is not activated by the completion); a complete mission keeps the sets it has. The base
        // Initialise clears the started flag, so the tracker persists its objectives to keep handling reports.
        [Test]
        public void InitialiseEmptiesTheSetsUnlessTheMissionIsComplete()
        {
            foreach ((bool complete, int[] expected) in new[] { (false, new[] { 1, 1, 0 }), (true, new[] { 1, 1, 1 }) })
            {
                WithWorld(world =>
                {
                    Rig rig = Build(world, 3, 2, r => Set(r.Tracker, "m_persistObjectiveCompletion", true)); // Reflection: authored toggle
                    rig.Tracker.StartMission(false);
                    Assert.That(rig.ActivationCounts, Is.EqualTo(new[] { 1, 1, 0 }));

                    rig.Mission.Save.Complete = complete;
                    rig.Tracker.Initialise(rig.Mission.State, null);
                    rig.Objectives[0].Action_ObjectiveComplete();
                    Assert.That(rig.ActivationCounts, Is.EqualTo(expected), "complete " + complete);
                });
            }
        }

        // Declarations from the DummyDll metadata (type 0x02000718): fields and flags in metadata order, the serialized
        // limit's attributes and default, the overrides' flags and the constructor last.
        [Test]
        public void DeclarationsKeepTheOriginalMembersFlagsAndAttributes()
        {
            AssertMembers(typeof(MissionSequencedInstancedTracker), 0x00100001,
                new[] { ("m_activeObjectiveLimit", 0x0001), ("m_inactiveObjectives", 0x0021), ("m_activeObjectives", 0x0021) },
                new[]
                {
                    ("Initialise", 0x00c6), ("StartMission", 0x00c6), ("StatusChanged", 0x00c4), ("OnDrawGizmosSelected", 0x0081), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(MissionSequencedInstancedTracker), 1, 2);
            Assert.That(typeof(MissionSequencedInstancedTracker).BaseType, Is.EqualTo(typeof(MissionInstancedTracker)));
            var limit = Field(typeof(MissionSequencedInstancedTracker), "m_activeObjectiveLimit");
            Assert.That(AttributeTypes(limit), Is.EqualTo(new[] { typeof(SerializeField), typeof(MinAttribute) }));
            Assert.That(limit.GetCustomAttributesData().Single(data => data.AttributeType == typeof(MinAttribute)).ConstructorArguments[0].Value,
                Is.EqualTo(1f));
        }
    }
}
