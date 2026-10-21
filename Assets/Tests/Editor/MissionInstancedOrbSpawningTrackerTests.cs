using System;
using System.Collections.Generic;
using System.Linq;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.MissionTrackerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // MissionInstancedOrbSpawningTracker.StatusChanged (0x5AF6E0) replaces the base handling and spawns the completion
    // orb (SpawnOrb 0x5AFD00) once every objective is complete. The orb prefab is a Collectable the Edit Mode
    // fixtures cannot instantiate, so the cases stop at the point where Object.Instantiate is handed no prefab.
    public sealed class MissionInstancedOrbSpawningTrackerTests
    {
        private sealed class Rig
        {
            internal World World;
            internal MissionInstancedOrbSpawningTracker Tracker;
            internal Mission Mission;
            internal List<MissionObjective> Objectives;
            internal CountingEvent Completed;
            internal CountingEvent Any;
            internal int StatusCallbacks;
        }

        private static void Run(int count, int target, Action<Rig> body, Action<Rig> beforeInitialise = null) => WithWorld(world =>
        {
            var rig = new Rig { World = world, Mission = world.NewMission(count, target) };
            rig.Tracker = world.NewTracker<MissionInstancedOrbSpawningTracker>();
            rig.Objectives = Enumerable.Range(0, count).Select(_ => world.NewObjective<MissionObjective>(rig.Tracker.transform)).ToList();
            Set(rig.Tracker, "m_missionObjectives", new List<MissionObjective>(rig.Objectives)); // Reflection: authored list
            rig.Completed = Author(rig.Tracker, "m_onCompleted");
            rig.Any = Author(rig.Tracker, "m_onAnyObjectiveCompleted");
            Author(rig.Tracker, "m_onLastObjective");
            rig.Tracker.OnStatusChanged = _ => rig.StatusCallbacks++;
            beforeInitialise?.Invoke(rig);
            rig.Tracker.Initialise(rig.Mission.State, null);
            body(rig);
        });

        // The override has no started-mission gate and no status callback (0x5AF776 goes straight to the set).
        [Test]
        public void PartialCompletionCountsWithoutAStartedMissionOrAStatusCallback()
        {
            Run(3, 2, rig =>
            {
                rig.Objectives[0].Action_ObjectiveComplete();
                Assert.That(rig.Tracker.ObjectivesComplete, Is.EqualTo(1));
                Assert.That(rig.Mission.State.Progress, Is.EqualTo(1));
                Assert.That(rig.Any.Count, Is.EqualTo(1));
                Assert.That(rig.StatusCallbacks, Is.EqualTo(0));

                rig.Objectives[1].Action_ObjectiveComplete();
                Assert.That(rig.Mission.State.Progress, Is.EqualTo(2), "the target of two does not spawn the orb");
                Assert.That(rig.Completed.Count, Is.EqualTo(0));
                Assert.That(rig.Mission.State.Complete, Is.False);

                rig.Objectives[1].OnObjectiveReset();
                Assert.That(rig.Tracker.ObjectivesComplete, Is.EqualTo(1));
            });
        }

        // 0x5AF8FD..0x5AF915: comparing the count with the whole list, the last completion detaches the tracker from
        // every objective and then spawns the orb, which faults here for lack of a prefab; the mission is not completed.
        [Test]
        public void CompletingEveryObjectiveDetachesTheTrackerAndTriesToSpawnTheOrbAtTheLastObjective()
        {
            Run(3, 2, rig =>
            {
                rig.Objectives[2].transform.position = new Vector3(7f, 8f, 9f);
                rig.Objectives[0].Action_ObjectiveComplete();
                rig.Objectives[1].Action_ObjectiveComplete();

                Assert.Catch<ArgumentException>(() => rig.Objectives[2].Action_ObjectiveComplete());

                Assert.That(rig.Mission.State.Progress, Is.EqualTo(3));
                Assert.That(rig.Objectives.Select(objective => objective.OnStatusChanged).ToArray(), Is.All.Null);
                Assert.That(rig.Completed.Count, Is.EqualTo(0), "completion waits for the orb to be collected");
                Assert.That(Get<LightweightTransform>(rig.Tracker, "m_orbSpawnLocation").Location, Is.EqualTo(new Vector3(7f, 8f, 9f)));
            });
        }

        [Test]
        public void AnAuthoredOrbLocationReplacesTheLastObjectivesPositionAsTheSpawnPoint()
        {
            Run(1, 1, rig =>
            {
                rig.Objectives[0].transform.position = new Vector3(7f, 8f, 9f);
                Assert.Catch<ArgumentException>(() => rig.Objectives[0].Action_ObjectiveComplete());
                Assert.That(Get<LightweightTransform>(rig.Tracker, "m_orbSpawnLocation").Location, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            }, rig =>
            {
                Transform anchor = rig.World.Owned.Host("Lucid orb location", false).transform;
                anchor.position = new Vector3(1f, 2f, 3f);
                Set(rig.Tracker, "m_orbLocation", anchor); // Reflection: authored orb location
            });
        }
    }
}
