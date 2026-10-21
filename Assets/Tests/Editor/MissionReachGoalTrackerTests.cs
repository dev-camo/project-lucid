using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.MissionTrackerTestSupport;

namespace ProjectLucid.Tests
{
    // MissionReachGoalTracker.Action_ReachedGoal (0x5BF960) is a tail call of the virtual MissionObjectivesCompleted.
    public sealed class MissionReachGoalTrackerTests
    {
        [Test]
        public void ReachingTheGoalCompletesTheMission()
        {
            WithWorld(world =>
            {
                Mission mission = world.NewMission();
                MissionReachGoalTracker tracker = world.NewTracker<MissionReachGoalTracker>();
                CountingEvent completed = Author(tracker, "m_onCompleted");
                tracker.Initialise(mission.State, null);
                Assert.That(mission.State.Complete, Is.False);

                tracker.Action_ReachedGoal();

                Assert.That(mission.State.Complete, Is.True);
                Assert.That(completed.Count, Is.EqualTo(1));
            });
        }
    }
}
