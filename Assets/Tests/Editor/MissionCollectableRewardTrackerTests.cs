using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.MissionTrackerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // MissionCollectableRewardTracker (Start 0x5AAC20, InitialiseProximityVolumes 0x5AB070, OnRewardCollected 0x5AB970,
    // MissionObjectivesCompleted 0x5AB9E0, the proximity trigger handlers 0x5ABB40..0x5ABB90, UpdateProximityTrackingState
    // 0x5ABA50 and the TrackCharacterProximity state machine 0x5ABD70). Rewards are real Collectables and the triggers
    // real CharacterEventTriggers; a collection is reported through the Collectable's public OnCollected callback.
    public sealed class MissionCollectableRewardTrackerTests
    {
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");

        private sealed class Rig
        {
            internal World World;
            internal MissionCollectableRewardTracker Tracker;
            internal Mission Mission;
            internal List<Collectable> Rewards;
            internal List<CharacterEventTrigger> Triggers;
            internal CountingEvent Completed;
        }

        private static Collectable NewReward(World world, Vector3 position)
        {
            GameObject host = world.Owned.Host("Lucid reward", false);
            host.transform.position = position;
            var reward = host.AddComponent<Collectable>();
            Set(reward, "m_type", CollectableType.Moon); // Reflection: authored serialized type
            return reward;
        }

        // The collider is disabled, so GetBounds answers with the snapshot Awake would have taken.
        private static CharacterEventTrigger NewTrigger(World world, Bounds bounds)
        {
            GameObject host = world.Owned.Host("Lucid proximity trigger", false);
            BoxCollider box = host.AddComponent<BoxCollider>();
            var trigger = host.AddComponent<CharacterEventTrigger>();
            box.enabled = false;
            Set(trigger, "m_bounds", bounds); // Reflection: the Awake snapshot, which Edit Mode never takes
            return trigger;
        }

        private static Rig Build(World world, Vector3[] rewardPositions, params Bounds[] triggerBounds)
        {
            var rig = new Rig { World = world, Mission = world.NewMission() };
            rig.Tracker = world.NewTracker<MissionCollectableRewardTracker>();
            rig.Rewards = rewardPositions.Select(position => NewReward(world, position)).ToList();
            rig.Triggers = triggerBounds.Select(bounds => NewTrigger(world, bounds)).ToList();
            Set(rig.Tracker, "m_collectableRewards", new List<Collectable>(rig.Rewards)); // Reflection: authored lists
            Set(rig.Tracker, "m_proximityTriggers", rig.Triggers.ToArray());
            rig.Completed = Author(rig.Tracker, "m_onCompleted");
            rig.Tracker.Initialise(rig.Mission.State, null);
            return rig;
        }

        private static void Run(Action<Rig> body, Vector3[] rewardPositions, params Bounds[] triggerBounds)
        {
            InitialiseCollectable();
            WithWorld(world => body(Build(world, rewardPositions, triggerBounds)));
        }

        private static readonly Vector3[] TwoRewards = { Vector3.zero, new Vector3(0f, 0f, 10f) };
        private static readonly Vector3[] RewardsAhead = { new Vector3(0f, 0f, 10f), new Vector3(0f, 0f, 20f) };
        private static readonly Bounds UnitCube = new Bounds(Vector3.zero, new Vector3(2f, 2f, 2f));

        private static int Subscriptions(Delegate callbacks, object tracker) =>
            callbacks == null ? 0 : callbacks.GetInvocationList().Count(callback => ReferenceEquals(callback.Target, tracker));

        [Test]
        public void StartRegistersTheRewardsAsOneGroupOnceAndTakesOverTheirRegistration()
        {
            Run(rig =>
            {
                Assert.That(rig.Rewards.Select(reward => reward.AutoRegisterToCollectionManager).ToArray(), Is.All.True);
                Call(rig.Tracker, "Start");
                Assert.That(rig.Rewards.Select(reward => reward.AutoRegisterToCollectionManager).ToArray(), Is.All.False);
                Assert.That(rig.Rewards.Select(reward => Subscriptions(reward.OnCollected, rig.Tracker)).ToArray(), Is.EqualTo(new[] { 1, 1 }));

                Assert.That(rig.World.Collectables.TryGetCollectableState(CollectableType.Moon, out CollectableState state), Is.True);
                Assert.That(state.Total, Is.EqualTo(2), "one registration of the group's size");
                Call(rig.Tracker, "Start");
                Assert.That(state.Total, Is.EqualTo(2), "a second start registers nothing more");
            }, TwoRewards, UnitCube);
        }

        // 0x5AB0DF..0x5AB2CC: the start distance is the extents magnitude of the union of all trigger bounds.
        [Test]
        public void StartDerivesTheStartDistanceFromTheUnionOfTheTriggerBounds()
        {
            Run(rig =>
            {
                Call(rig.Tracker, "Start");
                Assert.That(Get<float>(rig.Tracker, "m_proximityStartDistance"), Is.EqualTo(1.7320508f).Within(1e-5f), "extents 1,1,1");
            }, TwoRewards, UnitCube);
            Run(rig =>
            {
                Call(rig.Tracker, "Start");
                Assert.That(Get<float>(rig.Tracker, "m_proximityStartDistance"), Is.EqualTo(6.1644139f).Within(1e-4f), "union extents 6,1,1");
                foreach (CharacterEventTrigger trigger in rig.Triggers)
                {
                    Assert.That(Subscriptions(Get<Delegate>(trigger, "OnTriggerEnter"), rig.Tracker), Is.EqualTo(1));
                    Assert.That(Subscriptions(Get<Delegate>(trigger, "OnTriggerExit"), rig.Tracker), Is.EqualTo(1));
                }
            }, TwoRewards, UnitCube, new Bounds(new Vector3(10f, 0f, 0f), new Vector3(2f, 2f, 2f)));
        }

        [Test]
        public void CollectingEveryRewardCompletesTheMission()
        {
            Run(rig =>
            {
                Call(rig.Tracker, "Start");
                rig.Rewards[0].OnCollected(rig.Rewards[0]);
                Assert.That(rig.Completed.Count, Is.EqualTo(0));
                Assert.That(rig.Mission.State.Complete, Is.False);
                rig.Rewards[1].OnCollected(rig.Rewards[1]);
                Assert.That(rig.Completed.Count, Is.EqualTo(1));
                Assert.That(rig.Mission.State.Complete, Is.True);
            }, TwoRewards, UnitCube);
        }

        [Test]
        public void DestroyingTheTrackerLeavesTheRewardsAndTheTriggers()
        {
            Run(rig =>
            {
                Call(rig.Tracker, "Start");
                Call(rig.Tracker, "OnDestroy");
                Assert.That(rig.Rewards.Select(reward => Subscriptions(reward.OnCollected, rig.Tracker)).ToArray(), Is.EqualTo(new[] { 0, 0 }));
                Assert.That(Subscriptions(Get<Delegate>(rig.Triggers[0], "OnTriggerEnter"), rig.Tracker), Is.EqualTo(0));
                Assert.That(Subscriptions(Get<Delegate>(rig.Triggers[0], "OnTriggerExit"), rig.Tracker), Is.EqualTo(0));
            }, TwoRewards, UnitCube);
        }

        // 0x5ABB40..0x5ABBD9 with 0x5ABA50: entering adds the trigger once and asks for the tracking coroutine, which
        // needs an active tracker; leaving the last trigger stops tracking and zeroes all three measurements.
        [Test]
        public void EnteringATriggerTwiceTracksItOnceAndLeavingItResetsTheMeasurements()
        {
            InitialiseCollectable();
            WithCharacterWorld((world, manager, character) =>
            {
                Rig rig = Build(world, RewardsAhead, new Bounds(Vector3.zero, new Vector3(60f, 80f, 0f)));
                Call(rig.Tracker, "Start");
                List<CharacterEventTrigger> active = Get<List<CharacterEventTrigger>>(rig.Tracker, "m_activeProximityTriggers");

                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                rig.Tracker.OnProximityTriggerEnter(rig.Triggers[0]);
                rig.Tracker.OnProximityTriggerEnter(rig.Triggers[0]);
                Assert.That(active, Is.EqualTo(new[] { rig.Triggers[0] }));

                CharacterTriggerTestSupport.SetCurrent(manager, character, true);
                var tracking = (IEnumerator)Call(rig.Tracker, "TrackCharacterProximity");
                Set(character, "m_cameraForward", Vector3.forward); // Reflection: set by Character.Update from the camera
                tracking.MoveNext();
                Assert.That(rig.Tracker.NormalisedDistanceProximity, Is.Not.Zero);

                rig.Tracker.OnProximityTriggerExit(rig.Triggers[0]);
                Assert.That(active, Is.Empty);
                Assert.That(rig.Tracker.NormalisedDistanceProximity, Is.Zero);
                Assert.That(rig.Tracker.NormalisedWeightedProximity, Is.Zero);
                Assert.That(rig.Tracker.CollectionInProgress, Is.False);
            });
        }

        [Test]
        public void CompletingTheMissionStopsTrackingByEmptyingTheActiveTriggers()
        {
            InitialiseCollectable();
            WithWorld(world =>
            {
                Rig rig = Build(world, TwoRewards, UnitCube);
                Call(rig.Tracker, "Start");
                List<CharacterEventTrigger> active = Get<List<CharacterEventTrigger>>(rig.Tracker, "m_activeProximityTriggers");
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                rig.Tracker.OnProximityTriggerEnter(rig.Triggers[0]);
                Assert.That(active.Count, Is.EqualTo(1));

                rig.Rewards[0].OnCollected(rig.Rewards[0]);
                rig.Rewards[1].OnCollected(rig.Rewards[1]);
                Assert.That(active, Is.Empty);
            });
        }

        // 0x5ABDB8..0x5ABFF5. The start distance is 50 (extents 30,40,0), the distance weight 1 and the orientation
        // weight 2. Each row reads: reward z, camera forward, distance proximity, weighted proximity.
        [Test]
        public void TheMeasurementsFollowTheDistanceToTheFirstRewardAndTheCameraFacing()
        {
            InitialiseCollectable();
            WithCharacterWorld((world, manager, character) =>
            {
                Rig rig = Build(world, new[] { new Vector3(0f, 0f, 10f), Vector3.zero }, new Bounds(Vector3.zero, new Vector3(60f, 80f, 0f)));
                Call(rig.Tracker, "Start");
                Assert.That(Get<float>(rig.Tracker, "m_proximityStartDistance"), Is.EqualTo(50f));
                CharacterTriggerTestSupport.SetCurrent(manager, character, true);
                var rows = new (float rewardZ, Vector3 forward, float distance, float weighted)[]
                {
                    (10f, new Vector3(0f, 0f, 1f), 0.8f, 0.93333334f),
                    (10f, new Vector3(0f, 0f, -1f), 0.8f, 0.26666668f),
                    (10f, new Vector3(0f, 1f, 0f), 0.8f, 0.26666668f),
                    (10f, new Vector3(0.70710677f, 0f, 0.70710677f), 0.8f, 0.7380712f),
                    (100f, new Vector3(0f, 0f, 1f), -1f, 0.33333334f),
                };
                foreach ((float rewardZ, Vector3 forward, float distance, float weighted) in rows)
                {
                    rig.Rewards[0].transform.position = new Vector3(0f, 0f, rewardZ);
                    Set(character, "m_cameraForward", forward); // Reflection: set by Character.Update from the camera
                    var tracking = (IEnumerator)Call(rig.Tracker, "TrackCharacterProximity");
                    Assert.That(tracking.MoveNext(), Is.True);
                    Assert.That(tracking.Current, Is.Null);
                    string row = $"reward z {rewardZ}, forward {forward}";
                    Assert.That(rig.Tracker.NormalisedDistanceProximity, Is.EqualTo(distance).Within(1e-5f), row);
                    Assert.That(rig.Tracker.NormalisedWeightedProximity, Is.EqualTo(weighted).Within(1e-5f), row);
                    Assert.That(rig.Tracker.CollectionInProgress, Is.False, row);
                }
            });
        }

        // 0x5ABDB8: with no valid current character the tick yields without touching the measurements.
        [Test]
        public void WithoutACurrentCharacterTheMeasurementsAreLeftAlone()
        {
            InitialiseCollectable();
            WithCharacterWorld((world, manager, character) =>
            {
                Rig rig = Build(world, new[] { new Vector3(0f, 0f, 10f), Vector3.zero }, new Bounds(Vector3.zero, new Vector3(60f, 80f, 0f)));
                Call(rig.Tracker, "Start");
                Set(character, "m_cameraForward", new Vector3(0f, 0f, 1f)); // Reflection: set by Character.Update from the camera
                CharacterTriggerTestSupport.SetCurrent(manager, character, true);
                var tracking = (IEnumerator)Call(rig.Tracker, "TrackCharacterProximity");
                Assert.That(tracking.MoveNext(), Is.True);
                Assert.That(rig.Tracker.NormalisedDistanceProximity, Is.EqualTo(0.8f).Within(1e-5f), "seeded by a real tick");
                Assert.That(rig.Tracker.NormalisedWeightedProximity, Is.EqualTo(0.93333334f).Within(1e-5f));

                CharacterTriggerTestSupport.SetCurrent(manager, character, false);
                rig.Rewards[0].transform.position = new Vector3(0f, 0f, 100f);
                Assert.That(tracking.MoveNext(), Is.True);
                Assert.That(tracking.Current, Is.Null);
                Assert.That(rig.Tracker.NormalisedDistanceProximity, Is.EqualTo(0.8f).Within(1e-5f));
                Assert.That(rig.Tracker.NormalisedWeightedProximity, Is.EqualTo(0.93333334f).Within(1e-5f));
            });
        }

        [Test]
        public void DeclarationKeepsTheAuthoredDefaults()
        {
            InitialiseCollectable();
            WithWorld(world =>
            {
                var tracker = world.NewTracker<MissionCollectableRewardTracker>();
                Assert.That(new[]
                {
                    Get<float>(tracker, "m_proximityStartDistance"), Get<float>(tracker, "m_proximityDistanceWeighting"),
                    Get<float>(tracker, "m_proximityOrientationWeighting"),
                }, Is.EqualTo(new[] { 50f, 1f, 2f }));
                Assert.That(tracker.CollectableRewards, Is.Empty);
            });
        }
    }
}
