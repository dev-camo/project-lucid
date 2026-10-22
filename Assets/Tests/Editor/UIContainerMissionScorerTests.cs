using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.MissionTrackerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerMissionScorer (Game.Runtime 0x020008e7; OnEnable 0x6409B0, Update 0x640A10, ShowPoints 0x641100, ClearValues
    // 0x6409E0, TryInitialise 0x640F70, constructor 0x641180). Not covered: Update (0x640A10) once the container is initialised, because
    // TryInitialise only returns true for a set-up active mission whose tracker is ready and a loaded string table (the tracker's set-up is an asset load), and
    // m_initialised is lifecycle state that is not forced here. So three behaviours of Update are untested: a container with no
    // scorer hides its root every frame (0x640A68 -> 0x640B78), the first frame copies the values without ShowPointsAdded and
    // PointsAddedFromStreak (ShowPoints is covered directly), and the multiplier text is the product of the combo and combo-stage
    // multipliers (mulss at 0x640EF3).
    public sealed class UIContainerMissionScorerTests
    {
        private static UIContainerMissionScorer NewContainer(Owned owned, bool rootActive = false)
        {
            var container = owned.Host("Lucid mission scorer container", false).AddComponent<UIContainerMissionScorer>();
            var root = owned.Host("Lucid scorer root", rootActive).AddComponent<RectTransform>();
            Set(container, "m_enableRoot", root); // Reflection: serialized reference
            return container;
        }

        private static GameObject Root(UIContainerMissionScorer container) => Get<RectTransform>(container, "m_enableRoot").gameObject;

        private static Strings TextId(UIContainerMissionScorer container, string field) =>
            Get<Strings>(Get<UITextString>(container, field), "m_stringID");

        // The constructor 0x641180 builds the four texts with the string ids 0x55EEE990 (current score), 0x980FE3EE (added
        // score and added streak) and 0xFC101082 (current multiplier), and starts with the points display delayed (+0xF1 = 1).
        [Test]
        public void DeclarationBuildsTheScoreTextsAndDelaysShowingPoints()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<MissionManager>(); // the field initialiser's reference
                UIContainerMissionScorer container = NewContainer(owned);

                Assert.That((int)TextId(container, "m_currentScoreUI"), Is.EqualTo(1441720720));
                Assert.That((int)TextId(container, "m_addedScoreUI"), Is.EqualTo(-1743789074));
                Assert.That((int)TextId(container, "m_addedStreakUI"), Is.EqualTo(-1743789074));
                Assert.That((int)TextId(container, "m_currentMultiplierUI"), Is.EqualTo(-66056062));
                Assert.That(Get<bool>(container, "m_delayShowingPoints"), Is.True);
                Assert.That(Get<bool>(container, "m_initialised"), Is.False);
                Assert.That(Get<bool>(container, "m_hasMissionScorer"), Is.False);
            });
        }

        // OnEnable 0x6409B0 stores 0x0100 over +0xF0 (not initialised, points delayed) and hides the root.
        [Test]
        public void EnablingHidesTheRootAndDelaysShowingPointsAgain()
        {
            WithWorld(world =>
            {
                UIContainerMissionScorer container = NewContainer(world.Owned, rootActive: true);
                Call(container, "ShowPoints"); // clears the delay (the scorer's absent values are not read while delayed)
                Assert.That(Get<bool>(container, "m_delayShowingPoints"), Is.False);
                Assert.That(Root(container).activeSelf, Is.True);

                Call(container, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not

                Assert.That(Root(container).activeSelf, Is.False);
                Assert.That(Get<bool>(container, "m_delayShowingPoints"), Is.True);
                Assert.That(Get<bool>(container, "m_initialised"), Is.False);
            });
        }

        // ClearValues 0x6409E0 sets the root's active state to its argument.
        [Test]
        public void ClearValuesSetsTheRootsActiveState()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<MissionManager>();
                UIContainerMissionScorer container = NewContainer(owned);
                Call(container, "ClearValues", true); // Reflection: private UIContainerMissionScorer.ClearValues
                Assert.That(Root(container).activeSelf, Is.True);
                Call(container, "ClearValues", false); // Reflection: private UIContainerMissionScorer.ClearValues
                Assert.That(Root(container).activeSelf, Is.False);
            });
        }

        // ShowPoints 0x641100: while delayed it only clears the delay; afterwards the added-points binding follows the scorer's
        // ShowPointsAdded (the score display timer) and the streak binding its PointsAddedFromStreak (the streak timer), each from
        // its own timer. The scorer is assigned by TryInitialise from the active mission's tracker, whose set-up is an asset load.
        [Test]
        public void ShowPointsFollowsTheScorersTwoTimersOnceTheDelayIsOver()
        {
            // (combo points added, streak points added) -> (ShowPointsAdded, PointsAddedFromStreak)
            foreach ((bool combo, bool streak) in new[] { (false, false), (true, false), (false, true), (true, true) })
            {
                WithWorld(world =>
                {
                    Owned owned = world.Owned;
                    MissionScorer scorer = EnemyAbilityTestSupport.AddTimeScaled<MissionScorer>(owned.Host("Lucid mission scorer", false));
                    Author(scorer, "m_onAddPoints"); // Reflection: authored events and stages
                    Author(scorer, "m_onAddPointsFlat");
                    Author(scorer, "m_onLosePoints");
                    Set(scorer, "m_comboStages", new List<MissionScorerComboStage> { new MissionScorerComboStage() }); // Reflection: serialized m_comboStages the editor assigns
                    scorer.ResetComboStage();
                    if (combo) scorer.AddPointsFlat(10f, false);
                    if (streak) scorer.AddPointsFlat(20f, true);
                    UIContainerMissionScorer container = NewContainer(owned);
                    Set(container, "m_missionScorer", scorer); // Reflection: assigned by TryInitialise
                    string row = "combo " + combo + ", streak " + streak;
                    int notifications = 0;
                    container.ShowPointsAdded.AddListener(() => notifications++);
                    container.PointsAddedFromStreak.AddListener(() => notifications++);

                    Call(container, "ShowPoints"); // Reflection: private UIContainerMissionScorer.ShowPoints
                    Assert.That(Get<bool>(container, "m_delayShowingPoints"), Is.False, row);
                    Assert.That(notifications, Is.EqualTo(0), "the first call is delayed: " + row);

                    Call(container, "ShowPoints"); // Reflection: private UIContainerMissionScorer.ShowPoints
                    Assert.That(container.ShowPointsAdded.Value, Is.EqualTo(combo), row);
                    Assert.That(container.PointsAddedFromStreak.Value, Is.EqualTo(streak), row);
                    Assert.That(notifications, Is.EqualTo(2), row);
                });
            }
        }

        // TryInitialise 0x640F70: nothing is set up while the mission manager row has no system, while no mission is active, or
        // while the active mission's tracker is not set up. Update (0x640A10) then returns without writing a binding.
        [Test]
        public void NothingIsInitialisedWithoutASetUpActiveMission()
        {
            // No system in the mission manager row.
            Owned.Run(owned =>
            {
                owned.LeaseRow<MissionManager>();
                UIContainerMissionScorer container = NewContainer(owned, rootActive: true);
                int notifications = 0;
                container.PointsTotal.AddListener(() => notifications++);

                Assert.That(Call(container, "TryInitialise"), Is.EqualTo(false)); // Reflection: private UIContainerMissionScorer.TryInitialise
                Call(container, "Update"); // Reflection: Unity sends Update; Edit Mode does not

                Assert.That(Get<bool>(container, "m_initialised"), Is.False);
                Assert.That(Root(container).activeSelf, Is.True, "the root is left alone");
                Assert.That(notifications, Is.EqualTo(0));
            });

            // A mission manager with no active mission, then with an active mission whose state has no tracker.
            OriginalPersistentLevelObjectTests.WithMissions(true, (owned, missions) =>
            {
                owned.LeaseRow<TimeTrialLeaderboardManager>(); // MissionDefinition's field initialiser
                owned.LeaseRow<UIManager>(); // MissionState's field initialiser
                owned.LeaseRow<ChallengeManager>(); // MissionContextRegular's field initialiser
                UIContainerMissionScorer container = NewContainer(owned, rootActive: true);

                Assert.That(Call(container, "TryInitialise"), Is.EqualTo(false), "no active mission"); // Reflection: private UIContainerMissionScorer.TryInitialise

                var mission = Create<MissionDefinition>(owned);
                Set(mission, "m_guid", "lucid-scorer-mission"); // Reflection: authored GUID of a serialized definition
                Set(mission, "m_subMissions", new List<MissionDefinition>());
                missions.Manager.SetActiveMissionContext(new MissionContextRegular(mission, missions.CurrentLevel.LevelDefinition, false));
                Assert.That(missions.Manager.TryGetActiveMissionState(out MissionState state), Is.True);
                Assert.That(state.SetUpComplete, Is.False, "the state has data but no tracker");

                Assert.That(Call(container, "TryInitialise"), Is.EqualTo(false), "the tracker is not set up"); // Reflection: private UIContainerMissionScorer.TryInitialise
                Assert.That(Get<bool>(container, "m_initialised"), Is.False);
                Assert.That(Get<bool>(container, "m_hasMissionScorer"), Is.False);
                Assert.That(Root(container).activeSelf, Is.True, "the root is left alone");
            });
        }
    }
}
