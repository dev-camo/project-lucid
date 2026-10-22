using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.MissionTrackerTestSupport;
using static ProjectLucid.Tests.UiWidgetImageFixtures;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetForGameplayLevelDefinition (Game.Runtime 0x0200084d, getters 0x60D360 / 0x60D370), UIContainerSkipMissionIntro
    // (0x02000942, 0x67F210..0x67F2D0), UIContainerBonusZone (0x02000879, 0x619A70..0x61A4B0) and UIContainerActiveMission
    // (0x02000876, 0x617760..0x618EA0) as real components on inactive owned hosts over a real MissionManager and LevelManager.
    // Not covered: the skip with a current level (the current level comes from a loaded scene), the bonus zone's Start, SetupZone,
    // SetupLevels, Continue and level widget listener (they need a level configuration with a zone category, a progression with
    // collectable totals, real level widgets and, for Continue and Start, the App's storage), and the active mission's
    // UpdateMissionDescription for a locked archetype mission, PlayRankAudio with a rank and the rank branches that load rank
    // icons (they need the rank definitions of the DataManager and authored ranks).
    public sealed class MissionScreenContainerTests
    {
        // 0x60D360 / 0x60D370 return the two serialized fields, +0x10 and +0x18.
        [Test]
        public void ALevelWidgetPairReturnsItsSerializedLevelAndWidget()
        {
            Owned.Run(owned =>
            {
                var pair = new UIWidgetForGameplayLevelDefinition();
                Assert.That(pair.LevelDefinition, Is.Null);
                Assert.That(pair.Widget, Is.Null);

                var level = owned.Track(ScriptableObject.CreateInstance<GameplayLevelDefinition>());
                // The widget's field initialisers cache the UINavigationManager and DataManager references.
                owned.LeaseRow<UINavigationManager>();
                owned.LeaseRow<DataManager>();
                var widget = owned.Host("Lucid level widget", false).AddComponent<UIWidgetSDTLevel>();
                Set(pair, "m_gameplayLevelDefinition", level); // Reflection: serialized fields of the pair
                Set(pair, "m_widgetSDTLevel", widget);

                Assert.That(pair.LevelDefinition, Is.SameAs(level));
                Assert.That(pair.Widget, Is.SameAs(widget));
            });
        }

        // Start 0x67F210 takes the LevelManager (creating its row if absent); Action_OnFullSkip 0x67F290 asks it for the current level
        // and, with none, returns without touching a level.
        [Test]
        public void SkipMissionIntroTakesTheLevelManagerAndDoesNothingWithoutACurrentLevel()
        {
            WithWorld(world =>
            {
                var container = world.Owned.Host("Lucid skip mission intro", false).AddComponent<UIContainerSkipMissionIntro>();
                Assert.That(Get<LevelManager>(container, "m_levelManager"), Is.Null);

                Call(container, "Start");

                Assert.That(Get<LevelManager>(container, "m_levelManager"), Is.SameAs(world.Levels));
                Assert.That(world.Levels.CurrentLevelValid, Is.False);
                Assert.DoesNotThrow(container.Action_OnFullSkip);
            });
        }

        // Constructor 0x61A430 sets the numerical name offset to 1 before the ShowReveal binding is built.
        [Test]
        public void ABonusZoneNumbersItsLevelsFromOneAndStartsWithoutAReveal()
        {
            Owned.Run(owned =>
            {
                var zone = owned.Host("Lucid bonus zone", false).AddComponent<UIContainerBonusZone>();
                Assert.That(Get<int>(zone, "m_levelNumericalNameOffset"), Is.EqualTo(1));
                Assert.That(zone.ShowReveal, Is.Not.Null);
                Assert.That(zone.ShowReveal.Value, Is.False);
                Assert.That(Get<GameplayLevelDefinition>(zone, "m_selectedLevelDefinition"), Is.Null);
            });
        }

        // SelectLevelDefinition 0x61A410: only a widget turning on changes the selection; turning another widget off leaves the
        // selected level in place. The listener closure inlines the same store (0x61A4C0..0x61A4C9) and is not run here.
        [Test]
        public void OnlyAWidgetTurningOnChangesTheSelectedLevel()
        {
            Owned.Run(owned =>
            {
                var zone = owned.Host("Lucid bonus zone", false).AddComponent<UIContainerBonusZone>();
                var first = owned.Track(ScriptableObject.CreateInstance<GameplayLevelDefinition>());
                var second = owned.Track(ScriptableObject.CreateInstance<GameplayLevelDefinition>());

                Call(zone, "SelectLevelDefinition", false, first);
                Assert.That(Get<GameplayLevelDefinition>(zone, "m_selectedLevelDefinition"), Is.Null, "turning off selects nothing");

                Call(zone, "SelectLevelDefinition", true, first);
                Assert.That(Get<GameplayLevelDefinition>(zone, "m_selectedLevelDefinition"), Is.SameAs(first));

                Call(zone, "SelectLevelDefinition", false, second);
                Assert.That(Get<GameplayLevelDefinition>(zone, "m_selectedLevelDefinition"), Is.SameAs(first), "turning off keeps the selection");

                Call(zone, "SelectLevelDefinition", true, second);
                Assert.That(Get<GameplayLevelDefinition>(zone, "m_selectedLevelDefinition"), Is.SameAs(second));
            });
        }

        private static void RunActiveMission(Action<Owned, World, UIContainerActiveMission> body) => WithWorld(world =>
        {
            // The container's field initialisers cache the App, SaveManager and MissionManager references.
            world.Owned.LeaseRow<App>();
            body(world.Owned, world, world.Owned.Host("Lucid active mission", false).AddComponent<UIContainerActiveMission>());
        });

        // Refresh 0x617E00: without a mission state the panel is hidden, whether it is refreshed explicitly or through the no-argument
        // overload 0x6184D0 that refreshes the current state.
        [Test]
        public void RefreshingWithoutAMissionStateHidesTheMission()
        {
            RunActiveMission((owned, world, container) =>
            {
                // The constructor stores MISSION_TIMER_ELAPSED_FORMAT (0xC54DBD8D, 0x618F6D) as the time format.
                Assert.That(Get<Strings>(container, "m_timeFormatString"), Is.EqualTo(Strings.MISSION_TIMER_ELAPSED_FORMAT));
                container.ShowMission.Value = true;
                container.Refresh(null);
                Assert.That(container.ShowMission.Value, Is.False);
                Assert.That(Get<MissionState>(container, "m_currentMissionState"), Is.Null);

                container.ShowMission.Value = true;
                container.Refresh();
                Assert.That(container.ShowMission.Value, Is.False);
            });
        }

        // 0x617E8E: a state alone is not enough, the container also needs the mission manager from OnMissionManagerValid. The state is
        // still remembered (0x617E76).
        [Test]
        public void RefreshingBeforeTheMissionManagerIsKnownHidesTheMissionButKeepsTheState()
        {
            RunActiveMission((owned, world, container) =>
            {
                var state = new MissionState();
                container.ShowMission.Value = true;

                container.Refresh(state);

                Assert.That(container.ShowMission.Value, Is.False);
                Assert.That(Get<MissionState>(container, "m_currentMissionState"), Is.SameAs(state));
            });
        }

        // OnMissionIslandActive 0x6184B0 compares the state with the current one (0x6184B4) and refreshes only a different state.
        [Test]
        public void AnIslandActivationRefreshesOnlyForADifferentState()
        {
            RunActiveMission((owned, world, container) =>
            {
                var current = new MissionState();
                var other = new MissionState();
                container.Refresh(current);

                container.ShowMission.Value = true;
                Call(container, "OnMissionIslandActive", current);
                Assert.That(container.ShowMission.Value, Is.True, "the same state is not refreshed");

                Call(container, "OnMissionIslandActive", other);
                Assert.That(container.ShowMission.Value, Is.False, "a different state is refreshed");
                Assert.That(Get<MissionState>(container, "m_currentMissionState"), Is.SameAs(other));
            });
        }

        [Test]
        public void SettingTheRankAudioActiveRaisesItsFlagAndPlayingWithoutARankDoesNothing()
        {
            RunActiveMission((owned, world, container) =>
            {
                Assert.That(Get<bool>(container, "m_shouldPlayRankAudio"), Is.False);
                container.SetRankAudioActive();
                Assert.That(Get<bool>(container, "m_shouldPlayRankAudio"), Is.True);

                Assert.That(Get<MissionRank>(container, "m_currentMissionRank"), Is.Null);
                Assert.DoesNotThrow(container.PlayRankAudio, "0x618CD7: no rank, no audio manager is asked");
            });
        }

        // SynchroniseRankTimes 0x617B60 (run every frame by Update 0x617B50): both the option and a shown rank are required before the
        // mission manager is touched. The manager is not yet known here, so reaching it would fault.
        [TestCase(false, false, TestName = "RankTimesAreNotSynchronisedWithBothOff")]
        [TestCase(true, false, TestName = "RankTimesAreNotSynchronisedWithoutAShownRank")]
        [TestCase(false, true, TestName = "RankTimesAreNotSynchronisedWithoutTheOption")]
        public void RankTimesAreNotSynchronisedUnlessTheOptionAndTheShownRankAreBothOn(bool syncOption, bool showRank)
        {
            RunActiveMission((owned, world, container) =>
            {
                Set(container, "m_syncRankTimeWithMissionTime", syncOption); // Reflection: serialized option
                Set(container, "m_showRank", showRank); // Reflection: the state UpdateIconography sets from a time trial
                Assert.DoesNotThrow(() => Call(container, "Update"));
            });
        }

        // Control for the gate above: with both on, the mission manager (still unknown here) is asked, which faults.
        [Test]
        public void RankTimesReachTheMissionManagerWhenTheOptionAndTheShownRankAreBothOn()
        {
            RunActiveMission((owned, world, container) =>
            {
                Set(container, "m_syncRankTimeWithMissionTime", true); // Reflection: serialized option
                Set(container, "m_showRank", true); // Reflection: the state UpdateIconography sets from a time trial
                Exception thrown = Assert.Catch(() => Call(container, "Update"));
                if (thrown is TargetInvocationException)
                    thrown = thrown.InnerException;
                Assert.That(thrown, Is.InstanceOf<NullReferenceException>(), "0x617B92 / 0x617B9C passed, so m_missionManager is dereferenced");
            });
        }

        // AdjustForObjectives 0x618D70: a mission that fails at its time limit counts down from the limit plus the objectives' extra time
        // (single-precision adds, 0x618DA2 / 0x618DA7); any other mission keeps the time. 100.1f + 0.2f - 3.3f is 96.99999f in single
        // precision (a double evaluation would give 97).
        [TestCase(true, 90f, 5f, 62.5f, 32.5f, TestName = "AFailingMissionCountsDownFromItsLimitAndExtraTime")]
        [TestCase(true, 100.1f, 0.2f, 3.3f, 96.99999f, TestName = "TheCountdownIsEvaluatedInSinglePrecision")]
        [TestCase(false, 90f, 5f, 62.5f, 62.5f, TestName = "AMissionThatDoesNotFailAtTheLimitKeepsTheTime")]
        public void RankTimesCountDownFromTheLimitOnlyForAMissionThatFailsAtTheLimit(bool failOnLimit, float limit, float extra, float time, float expected)
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<SaveManager>(); // MissionDefinition's field initialisers
                owned.LeaseRow<TimeTrialLeaderboardManager>();
                string json = "{\"m_failOnTimeLimitReached\":" + (failOnLimit ? "true" : "false") + ",\"m_timeLimitSeconds\":" +
                              limit.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ",\"m_objectivesAdditionalTotalTimeSeconds\":" +
                              extra.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "}";
                var mission = Create<MissionDefinition>(owned, json);
                MethodInfo adjust = typeof(UIContainerActiveMission).GetMethod("AdjustForObjectives", BindingFlags.NonPublic | BindingFlags.Static);

                Assert.That(adjust.Invoke(null, new object[] { mission, time }), Is.EqualTo(expected));
            });
        }

        // UpdateIconography 0x618750: a mission that is not a time trial hides the rank, shows only its objective icon, clears the
        // icon background and foreground and returns before any rank binding is written (0x61889C).
        [Test]
        public void AMissionThatIsNotATimeTrialShowsOnlyItsObjectiveIcon()
        {
            RunActiveMission((owned, world, container) =>
            {
                var mission = Create<MissionDefinition>(owned);
                Sprite objective = NewSprite(owned);
                Sprite stale = NewSprite(owned);
                Set(mission, "m_missionObjectiveIcon", objective); // Reflection: serialized icon
                container.ShowRank.Value = true;
                container.MissionIconBackground.Value = stale;
                container.MissionIconForeground.Value = stale;
                container.LowestRankIcon.Value = stale;

                Call(container, "UpdateIconography", mission, mission, 12.5f, 3.5f);

                Assert.That(container.ShowRank.Value, Is.False);
                Assert.That(container.MissionIcon.Value, Is.SameAs(objective));
                Assert.That(container.MissionIconBackground.Value, Is.Null);
                Assert.That(container.MissionIconForeground.Value, Is.Null);
                Assert.That(container.LowestRankIcon.Value, Is.SameAs(stale), "the rank bindings are untouched");
                Assert.That(Get<bool>(container, "m_showRank"), Is.False);
            });
        }

        private const string TimerFormat = "{0}|{1}|{2}";
        private const string ActiveMissionGuid = "lucid-active-mission";

        private static readonly Dictionary<Strings, string> OwnedStrings = new Dictionary<Strings, string>
        {
            { Strings.MISSION_TIMER_ELAPSED_FORMAT, TimerFormat },
            { Strings.MISSION_TIMER_FORMAT_DESCRIPTION, TimerFormat },
            { (Strings)0, "Lucid mission" },
        };

        private static string TimedMissionJson(bool reachGoal = false) =>
            "{\"m_timeLimitSeconds\":90,\"m_objectivesAdditionalTotalTimeSeconds\":5,\"TheoreticalBestTimePossibleSeconds\":30,\"m_isReachGoalTracker\":" +
            (reachGoal ? "true" : "false") + "}";

        // UpdateIconography 0x618750, time-trial gate: m_showRank = IsTimeTrial && (!m_hideRankIfBestTimeIsInvalid || best > theoretical
        // best). The compare is ucomiss best, [definition+0xE8] (0x61879E) and seta (0x6187AA): strictly greater, false for NaN. A hidden
        // rank returns at 0x61889C before any rank binding is written; a shown one reaches the failed lookups (no ranks authored) that
        // clear the next rank (0x6188A7), best time (0x618AC0) and lowest rank (0x618B5A) icons. ShowRank is written from the same value.
        [TestCase(true, 30f, false, TestName = "ABestTimeEqualToTheTheoreticalBestHidesTheRank")]
        [TestCase(true, float.NaN, false, TestName = "ANaNBestTimeHidesTheRank")]
        [TestCase(true, 30.5f, true, TestName = "ABestTimeAboveTheTheoreticalBestShowsTheRank")]
        [TestCase(false, 0f, true, TestName = "TheHideOptionOffShowsTheRankWhateverTheBestTime")]
        public void ATimeTrialShowsItsRankOnlyForAValidBestTimeWhenTheOptionIsOn(bool hideIfInvalid, float bestTime, bool expectedShown)
        {
            RunActiveMission((owned, world, container) => OwnedStringTable.Run(OwnedStrings, () =>
            {
                var mission = Create<MissionDefinition>(owned, TimedMissionJson());
                Sprite objective = NewSprite(owned);
                Sprite stale = NewSprite(owned);
                Set(mission, "m_missionObjectiveIcon", objective); // Reflection: serialized icon
                Set(mission, "m_isBeingOverridden", true); // Reflection: set by UpdateOverrides for a completed mission with overrides
                Set(container, "m_hideRankIfBestTimeIsInvalid", hideIfInvalid); // Reflection: serialized option
                container.NextRankIcon.Value = stale;
                container.BestTimeRankIcon.Value = stale;
                container.LowestRankIcon.Value = stale;

                Call(container, "UpdateIconography", mission, mission, bestTime, 3.5f);

                Assert.That(container.ShowRank.Value, Is.EqualTo(expectedShown));
                Assert.That(Get<bool>(container, "m_showRank"), Is.EqualTo(expectedShown));
                Assert.That(container.MissionIcon.Value, Is.SameAs(objective));
                Sprite expectedRankIcon = expectedShown ? null : stale;
                Assert.That(container.NextRankIcon.Value, Is.EqualTo(expectedRankIcon), "a hidden rank returns before the rank bindings");
                Assert.That(container.BestTimeRankIcon.Value, Is.EqualTo(expectedRankIcon));
                Assert.That(container.LowestRankIcon.Value, Is.EqualTo(expectedRankIcon));
                if (expectedShown)
                {
                    // GetMissionTimerFormat(0f) is what the failed lookups format: a time of zero or less gives "-", "--", "--".
                    Assert.That(container.NextRankTimeToBeatFormatted.Value, Is.EqualTo("-|--|--"));
                    Assert.That(container.LowestRankTimeElapsedFormatted.Value, Is.EqualTo("-|--|--"));
                }
            }));
        }

        private static void RunActiveMissionInMissions(Action<Owned, OriginalPersistentLevelObjectTests.Missions, UIContainerActiveMission> body) =>
            OriginalPersistentLevelObjectTests.WithMissions(true, (owned, missions) =>
            {
                // The container's field initialisers cache the App, SaveManager and MissionManager references; the mission definition,
                // state and context field initialisers and the rank cache these rows.
                owned.LeaseRow<App>();
                owned.LeaseRow<TimeTrialLeaderboardManager>();
                owned.LeaseRow<UIManager>();
                owned.LeaseRow<ChallengeManager>();
                owned.LeaseRow<DataManager>();
                body(owned, missions, owned.Host("Lucid active mission", false).AddComponent<UIContainerActiveMission>());
            });

        // Publishes a regular context for a timed mission through the real manager (which loads its state), with a saved best time.
        private static MissionDefinition ActivateMission(Owned owned, OriginalPersistentLevelObjectTests.Missions missions, bool timeTrial,
            bool reachGoal = false, float bestTime = 0f)
        {
            var mission = Create<MissionDefinition>(owned, TimedMissionJson(reachGoal));
            Set(mission, "m_guid", ActiveMissionGuid); // Reflection: authored GUID of a serialized definition
            Set(mission, "m_subMissions", new List<MissionDefinition>()); // Reflection: authored (empty) list
            missions.Save.CurrentSave.GetOrCreateLevelData(missions.CurrentLevel.LevelDefinition.GetGUID())
                .GetOrCreateMissionData(ActiveMissionGuid).BestTimeSeconds = bestTime;
            missions.Manager.SetActiveMissionContext(new MissionContextRegular(mission, missions.CurrentLevel.LevelDefinition, false));
            // Loading the state ran UpdateOverrides, which cleared the flag; a time trial is a completed mission with overrides.
            Set(mission, "m_isBeingOverridden", timeTrial); // Reflection: set by UpdateOverrides
            return mission;
        }

        // SynchroniseRankTimes 0x617B60 after the gate: the current rank is kept for RankType.D (cmp [rank+0x10], 0x5C4C95FB at 0x617BD7)
        // and while its time is not below the elapsed time (ucomiss rank.TimeInSeconds, elapsed; jae at 0x617BE9, taken on equality,
        // not taken for NaN). Otherwise the lookup runs with the field itself as the out parameter (lea rsi, [rbx+0x158], 0x617BF2), and
        // with no authored ranks it fails and clears the field. The mission here has not started, so the elapsed time is 0.
        [TestCase(RankType.C, 0f, true, TestName = "ARankTimeEqualToTheElapsedTimeIsKept")]
        [TestCase(RankType.C, 1f, true, TestName = "ARankTimeAfterTheElapsedTimeIsKept")]
        [TestCase(RankType.C, -1f, false, TestName = "ARankTimeBeforeTheElapsedTimeIsLookedUpAgain")]
        [TestCase(RankType.C, float.NaN, false, TestName = "ANaNRankTimeIsLookedUpAgain")]
        [TestCase(RankType.D, -1f, true, TestName = "TheLowestRankIsNeverLookedUpAgain")]
        public void TheDisplayedRankIsLookedUpAgainOnlyWhenTheMissionRunsPastIt(RankType rankType, float rankTime, bool kept)
        {
            RunActiveMissionInMissions((owned, missions, container) =>
            {
                ActivateMission(owned, missions, timeTrial: false);
                var rank = new MissionRank();
                Set(rank, "m_rankType", rankType); // Reflection: serialized rank data
                Set(rank, "m_timeInSeconds", rankTime); // Reflection: computed by the editor Validate
                Set(container, "m_missionManager", missions.Manager); // Reflection: set by OnMissionManagerValid
                Set(container, "m_syncRankTimeWithMissionTime", true); // Reflection: serialized option
                Set(container, "m_showRank", true); // Reflection: the state UpdateIconography sets from a time trial
                Set(container, "m_currentMissionRank", rank); // Reflection: the rank UpdateIconography found

                Call(container, "Update");

                if (kept)
                    Assert.That(Get<MissionRank>(container, "m_currentMissionRank"), Is.SameAs(rank));
                else
                    Assert.That(Get<MissionRank>(container, "m_currentMissionRank"), Is.Null);
            });
        }

        // Refresh 0x617E00 with a live state. Total time is the limit plus the objectives' extra time (0x617FE3), and the unstarted timer
        // leaves the elapsed time 0 (0x617F0B..0x617F33). NewBestTime is best >= elapsed (ucomiss; setae at 0x6181E2: true for a best
        // time of 0, false for a negative one). ShowBestTime is the conjunction m_showBestTime && m_showRank && IsTimeTrial && best > 0
        // && IsReachGoalTracker (0x618185..0x6181BA); each row has one operand off, except the first.
        [TestCase(true, true, true, 12f, true, true, "0|12|00", TestName = "ABestTimeIsShownWhenEveryConditionHolds")]
        [TestCase(false, true, true, 12f, false, true, "0|12|00", TestName = "TheBestTimeIsHiddenWithTheOptionOff")]
        [TestCase(true, false, true, 12f, false, true, "0|12|00", TestName = "TheBestTimeIsHiddenForAMissionThatIsNotATimeTrial")]
        [TestCase(true, true, false, 12f, false, true, "0|12|00", TestName = "TheBestTimeIsHiddenWithoutAReachGoalTracker")]
        [TestCase(true, true, true, 0f, false, true, "-|--|--", TestName = "ABestTimeOfZeroIsNotShownButIsStillANewBest")]
        [TestCase(true, true, true, -1f, false, false, "-|--|--", TestName = "ANegativeBestTimeIsNotANewBest")]
        public void RefreshingAnActiveMissionPublishesItsTimesAndBestTimeState(bool showBestTimeOption, bool timeTrial, bool reachGoal,
            float bestTime, bool expectedShowBestTime, bool expectedNewBestTime, string expectedBestFormatted)
        {
            RunActiveMissionInMissions((owned, missions, container) => OwnedStringTable.Run(OwnedStrings, () =>
            {
                ActivateMission(owned, missions, timeTrial, reachGoal, bestTime);
                Assert.That(missions.Manager.TryGetActiveMissionState(out MissionState state), Is.True);
                Set(container, "m_missionManager", missions.Manager); // Reflection: set by OnMissionManagerValid
                Set(container, "m_showBestTime", showBestTimeOption); // Reflection: serialized option

                container.Refresh(state);

                Assert.That(container.ShowMission.Value, Is.True);
                Assert.That(container.TimeTotalSeconds.Value, Is.EqualTo(95f));
                Assert.That(container.TimeElapsedFormatted.Value, Is.EqualTo("-|--|--"), "an unstarted timer has no elapsed time");
                Assert.That(container.TimeRemainingFormatted.Value, Is.EqualTo("1|35|00"));
                Assert.That(container.MissionDescription.Value, Is.EqualTo("Lucid mission"));
                Assert.That(container.ShowRank.Value, Is.EqualTo(timeTrial));
                Assert.That(container.ShowBestTime.Value, Is.EqualTo(expectedShowBestTime));
                Assert.That(container.NewBestTime.Value, Is.EqualTo(expectedNewBestTime));
                Assert.That(container.BestTimeElapsedFormatted.Value, Is.EqualTo(expectedBestFormatted));
                Assert.That(container.IsChallenge.Value, Is.False);
            }));
        }

        // SetIcon 0x618DC0 loads the asset into the binding and keeps it; OnDestroy 0x6178B0 removes the string-table handler, unloads
        // every kept asset and detaches from the mission manager. The identifier is unset, so it is not closed.
        [Test]
        public void IconsAreKeptAndReleasedWhenTheContainerIsDestroyed()
        {
            RunActiveMission((owned, world, container) =>
            {
                FastAction saved = StringTable.StringsTableHandlers;
                try
                {
                    ManagedAddressableAsset<Sprite> first = LoadedWrapper(owned, out Sprite firstSprite);
                    ManagedAddressableAsset<Sprite> second = LoadedWrapper(owned, out Sprite secondSprite);
                    Call(container, "SetIcon", container.NextRankIcon, first);
                    Call(container, "SetIcon", container.MissionIcon, second);
                    Assert.That(container.NextRankIcon.Value, Is.SameAs(firstSprite));
                    Assert.That(container.MissionIcon.Value, Is.SameAs(secondSprite));
                    Assert.That(Get<List<ManagedAddressableAsset<Sprite>>>(container, "m_managedAddressableSprites"), Is.EqualTo(new[] { first, second }));
                    Assert.That(Count(first), Is.EqualTo(1));

                    Call(container, "OnDestroy");

                    Assert.That(Count(first), Is.EqualTo(0));
                    Assert.That(Count(second), Is.EqualTo(0));
                    Assert.That(IsCached(first), Is.False);
                }
                finally
                {
                    StringTable.StringsTableHandlers = saved;
                }
            });
        }

        // Start 0x617760: the mission manager is taken as soon as it is valid (OnMissionManagerValid 0x617D00) and Refresh follows the
        // string table. With no active mission context there is no state to show or follow.
        [Test]
        public void StartTakesTheMissionManagerAndFollowsTheStringTable()
        {
            RunActiveMission((owned, world, container) =>
            {
                // The handler list is a shared mutable object (FastAction is changed in place): remember the incoming reference and
                // its subscribers, take away only the exact subscription Start adds, and put a null incoming value back as null.
                FastAction incoming = StringTable.StringsTableHandlers;
                int incomingCount = incoming == null ? 0 : incoming.GetInvocationList().Count();
                System.Action refresh = container.Refresh;
                int restoredCount = -1;
                bool completed = false;
                try
                {
                    container.ShowMission.Value = true;

                    Call(container, "Start");

                    Assert.That(Get<MissionManager>(container, "m_missionManager"), Is.SameAs(world.Missions));
                    Assert.That(container.ShowMission.Value, Is.True, "no active mission state: nothing was refreshed");
                    Assert.That(StringTable.StringsTableHandlers.GetInvocationList()
                        .Count(handler => ReferenceEquals(handler.Target, container) && handler.Method.Name == "Refresh"), Is.EqualTo(1));
                    completed = true;
                }
                finally
                {
                    StringTable.StringsTableHandlers -= refresh;
                    StringTable.StringsTableHandlers = incoming;
                    restoredCount = StringTable.StringsTableHandlers == null ? 0 : StringTable.StringsTableHandlers.GetInvocationList().Count();
                }
                // Asserted after the restore, and only for a completed body, so it cannot replace the body's own failure.
                if (completed) Assert.That(restoredCount, Is.EqualTo(incomingCount), "the case leaves the handler list as it found it");
            });
        }

        // A subscriber the case did not add is not the case's to touch: Start adds its own handler, the case takes away only that
        // one and the sentinel is neither called nor dropped.
        [Test]
        public void StartLeavesAForeignStringTableSubscriberSubscribedAndUncalled()
        {
            RunActiveMission((owned, world, container) =>
            {
                FastAction incoming = StringTable.StringsTableHandlers;
                int sentinelCalls = 0;
                System.Action sentinel = () => sentinelCalls++;
                System.Action refresh = container.Refresh;
                bool completed = false;
                bool sentinelSubscribed = false;
                try
                {
                    StringTable.StringsTableHandlers = null;
                    StringTable.StringsTableHandlers += sentinel;
                    container.ShowMission.Value = true;

                    Call(container, "Start");
                    completed = true;
                }
                finally
                {
                    StringTable.StringsTableHandlers -= refresh;
                    sentinelSubscribed = StringTable.StringsTableHandlers != null
                        && StringTable.StringsTableHandlers.GetInvocationList().Contains(sentinel);
                    StringTable.StringsTableHandlers = incoming;
                }
                if (completed)
                {
                    Assert.That(sentinelCalls, Is.EqualTo(0), "Start does not raise the string table handlers");
                    Assert.That(sentinelSubscribed, Is.True, "removing the container's handler leaves the foreign subscriber");
                }
            });
        }
    }
}
