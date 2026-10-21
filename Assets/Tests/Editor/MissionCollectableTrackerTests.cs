using System;
using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.MissionTrackerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // MissionCollectableTracker (Initialise 0x5AC110, InitialiseCollectableManager 0x5AC780, OnCollectableStateUpdate
    // 0x5ACB80, the manager hooks 0x5AC8C0 and 0x5ACD50). The collectable manager's public state callback is raised
    // with real CollectableState records, as the manager does after a change.
    public sealed class MissionCollectableTrackerTests
    {
        private sealed class Rig
        {
            internal World World;
            internal MissionCollectableTracker Tracker;
            internal Mission Mission;
            internal CountingEvent Completed;
            internal CollectableState Ring;

            // Moves the ring record to the amount, then raises the manager's callback as ChangeCollectableAmount would.
            internal void Raise(int collected, CollectableSource source = CollectableSource.None, CollectableType type = CollectableType.Ring,
                CollectableState state = null)
            {
                state = state ?? Ring;
                state.AdjustCollected(collected - state.Collected, true);
                World.Collectables.OnCollectableStateUpdate(type, state, new CollectableChangeMetadata { Source = source });
            }
        }

        private static void Run(Action<Rig> body, int target = 5, bool ignoreLosses = true, CollectableSource[] sources = null,
            int alreadyCollected = 0, bool previouslyComplete = false) => WithWorld(world =>
        {
            var rig = new Rig { World = world };
            world.Collectables.RegisterCollectable(CollectableType.Ring, 100);
            world.Collectables.TryGetCollectableState(CollectableType.Ring, out rig.Ring);
            rig.Ring.AdjustCollected(alreadyCollected, true);
            rig.Mission = world.NewMission(target, target);
            if (previouslyComplete) rig.Mission.Save.MarkComplete(target);
            rig.Tracker = world.NewTracker<MissionCollectableTracker>();
            Set(rig.Tracker, "m_collectableType", CollectableType.Ring); // Reflection: authored serialized settings
            Set(rig.Tracker, "m_targetAmount", target);
            Set(rig.Tracker, "m_ignoreLosses", ignoreLosses);
            Set(rig.Tracker, "m_collectableSources", sources ?? new CollectableSource[0]);
            rig.Completed = Author(rig.Tracker, "m_onCompleted");
            rig.Tracker.Initialise(rig.Mission.State, null);
            body(rig);
        });

        // 0x5ACBC2..0x5ACBF2: the change is measured against the last amount seen, and that amount moves before the
        // ignore-losses test, so an ignored loss still lowers the baseline the next gain is measured from.
        [Test]
        public void TheTrackedAmountGrowsByTheChangesAndAnIgnoredLossStillMovesTheBaseline()
        {
            Run(rig =>
            {
                rig.Raise(2);
                Assert.That(rig.Mission.State.Progress, Is.EqualTo(2));
                rig.Raise(2);
                Assert.That(rig.Mission.State.Progress, Is.EqualTo(2), "no change");
                rig.Raise(1);
                Assert.That(rig.Mission.State.Progress, Is.EqualTo(2), "the loss is ignored");
                rig.Raise(3);
                Assert.That(rig.Mission.State.Progress, Is.EqualTo(4), "measured from the lowered baseline of 1");
                Assert.That(rig.Mission.State.Complete, Is.False);

                rig.Raise(4);
                Assert.That(rig.Mission.State.Progress, Is.EqualTo(5));
                Assert.That(rig.Mission.State.Complete, Is.True);
                Assert.That(rig.Completed.Count, Is.EqualTo(1));
            });
        }

        [Test]
        public void ReachingTheTargetWithALargerGainClampsTheProgressToTheTarget()
        {
            Run(rig =>
            {
                rig.Raise(4);
                rig.Raise(9);
                Assert.That(rig.Mission.State.Progress, Is.EqualTo(5));
                Assert.That(rig.Completed.Count, Is.EqualTo(1));
            });
        }

        // 0x5ACC20..0x5ACCC0: the total is clamped between 0 and the target, and losses count when not ignored.
        [Test]
        public void LossesLowerTheTotalWhenNotIgnoredAndNeverBelowZero()
        {
            Run(rig =>
            {
                rig.Raise(6);
                Assert.That(rig.Mission.State.Progress, Is.EqualTo(1), "measured from the amount already collected, 5");
                rig.Raise(1);
                Assert.That(rig.Mission.State.Progress, Is.EqualTo(0), "a loss of 5 from a total of 1 stops at zero");
                rig.Raise(4);
                Assert.That(rig.Mission.State.Progress, Is.EqualTo(3), "the gain adds to zero, not to a negative total");
            }, ignoreLosses: false, alreadyCollected: 5);
        }

        // 0x5ACBFF..0x5ACC1A: sources outside a non-empty list are skipped, again after the baseline has moved.
        [Test]
        public void OnlyChangesFromTheAuthoredSourcesCountButEveryChangeMovesTheBaseline()
        {
            Run(rig =>
            {
                rig.Raise(2, CollectableSource.Level);
                Assert.That(rig.Mission.State.Progress, Is.EqualTo(0), "an unlisted source");
                rig.Raise(3, CollectableSource.MissionReward);
                Assert.That(rig.Mission.State.Progress, Is.EqualTo(1), "the unlisted change moved the baseline to 2");
            }, sources: new[] { CollectableSource.MissionReward });
        }

        [Test]
        public void ChangesToOtherCollectableTypesAreIgnored()
        {
            Run(rig =>
            {
                var orbs = new CollectableState();
                orbs.AddCollectable(100);
                rig.Raise(3, type: CollectableType.Orb, state: orbs);
                Assert.That(rig.Mission.State.Progress, Is.EqualTo(0));
                rig.Raise(1);
                Assert.That(rig.Mission.State.Progress, Is.EqualTo(1), "the other type did not move the baseline");
            });
        }

        // 0x5AC17D..0x5AC195: a previously complete mission is left alone and the tracker never joins the manager.
        [Test]
        public void SetupJoinsTheManagerAndRestartsTheProgressOnlyForAnIncompleteMission()
        {
            Run(rig =>
            {
                Assert.That(rig.Mission.State.Progress, Is.EqualTo(0));
                Assert.That(rig.Mission.State.Target, Is.EqualTo(5));
                Assert.That(Subscriptions(rig), Is.EqualTo(1));
            });
            Run(rig =>
            {
                Assert.That(rig.Mission.State.Target, Is.EqualTo(0), "the progress was not restarted with the target");
                Assert.That(Subscriptions(rig), Is.EqualTo(0));
            }, previouslyComplete: true);
        }

        // 0x5AC860..0x5AC890: the amount already collected is the baseline, so only later gains count.
        [Test]
        public void TheAmountAlreadyCollectedIsTheBaseline()
        {
            Run(rig =>
            {
                rig.Raise(6);
                Assert.That(rig.Mission.State.Progress, Is.EqualTo(2));
            }, alreadyCollected: 4);
        }

        // 0x5AC8A0 and 0x5ACD50: completion and destruction both leave the manager.
        [Test]
        public void CompletionAndDestructionBothLeaveTheManager()
        {
            Run(rig =>
            {
                Assert.That(Subscriptions(rig), Is.EqualTo(1));
                rig.Raise(5);
                Assert.That(Subscriptions(rig), Is.EqualTo(0), "completing the mission detached the tracker");
            });
            Run(rig =>
            {
                Call(rig.Tracker, "OnDestroy");
                Assert.That(Subscriptions(rig), Is.EqualTo(0));
            });
        }

        [Test]
        public void IgnoringLossesIsTheDefault()
        {
            WithWorld(world => Assert.That(Get<bool>(world.NewTracker<MissionCollectableTracker>(), "m_ignoreLosses"), Is.True));
        }

        private static int Subscriptions(Rig rig)
        {
            Action<CollectableType, CollectableState, CollectableChangeMetadata> callbacks = rig.World.Collectables.OnCollectableStateUpdate;
            if (callbacks == null) return 0;
            int count = 0;
            foreach (Delegate callback in callbacks.GetInvocationList())
                if (ReferenceEquals(callback.Target, rig.Tracker)) count++;
            return count;
        }
    }
}
