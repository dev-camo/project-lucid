using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
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
    // SetupLevels, Continue and level widget listener (they need a save, a progression, a level configuration and the App's
    // storage), and the active mission's Refresh with a live mission state, UpdateMissionDescription, the time-trial rank branches of
    // UpdateIconography, PlayRankAudio with a rank and the rank synchronisation itself (they need an active mission context with a
    // saved mission, the rank definitions of the DataManager and a timed mission tracker).
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

        // SelectLevelDefinition 0x61A410 (and the listener closure 0x61A4B0): only a widget turning on changes the selection; turning
        // another widget off leaves the selected level in place.
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
                FastAction saved = StringTable.StringsTableHandlers;
                try
                {
                    container.ShowMission.Value = true;

                    Call(container, "Start");

                    Assert.That(Get<MissionManager>(container, "m_missionManager"), Is.SameAs(world.Missions));
                    Assert.That(container.ShowMission.Value, Is.True, "no active mission state: nothing was refreshed");
                    Assert.That(StringTable.StringsTableHandlers.GetInvocationList()
                        .Count(handler => ReferenceEquals(handler.Target, container) && handler.Method.Name == "Refresh"), Is.EqualTo(1));
                }
                finally
                {
                    StringTable.StringsTableHandlers = saved;
                }
            });
        }
    }
}
