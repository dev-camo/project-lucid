using System;
using System.Collections.Generic;
using System.Linq;
using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.MissionTrackerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // MissionBossTracker (Initialise 0x5AA5F0 with its closure 0x5AAA50, OnBossDefeated 0x5AA800, ResetMission 0x5AA850,
    // OnDestroy 0x5AA8F0) against a real BossManager, whose public defeat and hard-failure callbacks the tracker joins.
    public sealed class MissionBossTrackerTests
    {
        private static void WithBoss(int bossesToDefeat, Action<World, BossManager, MissionBossTracker, Mission, CountingEvent> body,
            Action<MissionBossTracker> authored = null) => WithWorld(world =>
        {
            BossManager boss = world.Owned.Register(world.Owned.Host("Lucid boss manager", false).AddComponent<BossManager>());
            MissionBossTracker tracker = world.NewTracker<MissionBossTracker>();
            Set(tracker, "m_bossesToDefeat", bossesToDefeat); // Reflection: authored serialized count
            authored?.Invoke(tracker);
            CountingEvent completed = Author(tracker, "m_onCompleted");
            Mission mission = world.NewMission();
            body(world, boss, tracker, mission, completed);
        });

        private static int Subscriptions(Delegate callbacks, object tracker) =>
            callbacks.GetInvocationList().Count(callback => ReferenceEquals(callback.Target, tracker));

        // 0x5AA812 cmp then jge, and 0x5AA83A setge: reaching the count completes, and so does every defeat after it.
        [Test]
        public void ReachingTheAuthoredNumberOfBossDefeatsCompletesTheMissionAndSoDoesEachLaterDefeat()
        {
            WithBoss(2, (world, boss, tracker, mission, completed) =>
            {
                tracker.Initialise(mission.State, null);
                boss.OnBossDefeated();
                Assert.That(completed.Count, Is.EqualTo(0));
                Assert.That(mission.State.Complete, Is.False);

                boss.OnBossDefeated();
                Assert.That(completed.Count, Is.EqualTo(1));
                Assert.That(mission.State.Complete, Is.True);

                boss.OnBossDefeated();
                Assert.That(completed.Count, Is.EqualTo(2), "greater than the count still completes");
            });
        }

        [Test]
        public void ANonPositiveCountCompletesOnTheFirstDefeat()
        {
            WithBoss(0, (world, boss, tracker, mission, completed) =>
            {
                tracker.Initialise(mission.State, null);
                boss.OnBossDefeated();
                Assert.That(completed.Count, Is.EqualTo(1));
            });
        }

        // 0x5AA850: the defeat count restarts at zero before the base reset.
        [Test]
        public void ResettingTheMissionRestartsTheDefeatCount()
        {
            WithBoss(2, (world, boss, tracker, mission, completed) =>
            {
                tracker.Initialise(mission.State, null);
                boss.OnBossDefeated();
                tracker.ResetMission();
                boss.OnBossDefeated();
                Assert.That(completed.Count, Is.EqualTo(0), "one defeat since the reset");
                boss.OnBossDefeated();
                Assert.That(completed.Count, Is.EqualTo(1));
            });
        }

        // 0x5AAB00..0x5AAB7C: the tracker joins both boss callbacks; a hard failure fails the mission only when the
        // authored behaviour shows the results screen, and a failed mission cannot then be completed.
        [Test]
        public void ABossHardFailureFailsTheMissionOnlyWhenTheFailureBehaviourShowsResults()
        {
            foreach ((MissionTracker.FailStateBehaviourType behaviour, bool complete) in new[]
            {
                (MissionTracker.FailStateBehaviourType.None, true), (MissionTracker.FailStateBehaviourType.ShowResultsScreen, false),
            })
            {
                WithBoss(1, (world, boss, tracker, mission, completed) =>
                {
                    tracker.Initialise(mission.State, null);
                    boss.OnBossHardFailure();
                    boss.OnBossDefeated();
                    Assert.That(mission.State.Complete, Is.EqualTo(complete), "failure behaviour " + behaviour);
                    Assert.That(completed.Count, Is.EqualTo(1), "the completion event is not gated by the state");
                }, tracker => Set(tracker, "m_failStateBehaviour", behaviour)); // Reflection: authored failure behaviour
            }
        }

        [Test]
        public void TheTrackerJoinsTheBossCallbacksOnceAndLeavesThemWhenDestroyed()
        {
            WithBoss(2, (world, boss, tracker, mission, completed) =>
            {
                Assert.That(Subscriptions(boss.OnBossDefeated, tracker), Is.EqualTo(0));
                tracker.Initialise(mission.State, null);
                Assert.That(Subscriptions(boss.OnBossDefeated, tracker), Is.EqualTo(1));
                Assert.That(Subscriptions(boss.OnBossHardFailure, tracker), Is.EqualTo(1));
                Assert.That(Subscriptions(boss.OnBossFullyDefeated, tracker), Is.EqualTo(0), "only two of the three callbacks");

                Call(tracker, "OnDestroy");
                Assert.That(Subscriptions(boss.OnBossDefeated, tracker), Is.EqualTo(0));
                Assert.That(Subscriptions(boss.OnBossHardFailure, tracker), Is.EqualTo(0));
            });
        }

        // 0x5AA6F8..0x5AA74B and 0x5AA88B..0x5AA8D8: ring removal asks the collectable manager to force-deactivate
        // the Ring type on setup and on every reset, and only when the tracker was authored to remove rings.
        [Test]
        public void RemovingRingsDeactivatesTheRingCollectablesOnSetupAndOnEachReset()
        {
            foreach (bool removeRings in new[] { true, false })
            {
                WithBoss(1, (world, boss, tracker, mission, completed) =>
                {
                    var deactivated = new List<CollectableType>();
                    world.Collectables.OnCollectableForceDeactivate += deactivated.Add;
                    tracker.Initialise(mission.State, null);
                    Assert.That(deactivated, Is.EqualTo(removeRings ? new[] { CollectableType.Ring } : new CollectableType[0]));
                    tracker.ResetMission();
                    Assert.That(deactivated, Is.EqualTo(removeRings ? new[] { CollectableType.Ring, CollectableType.Ring } : new CollectableType[0]));
                }, tracker => Set(tracker, "m_removeRingsFromArena", removeRings)); // Reflection: authored toggle
            }
        }
    }
}
