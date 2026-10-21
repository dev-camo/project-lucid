using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.MissionTrackerTestSupport;
using static ProjectLucid.Tests.UiWidgetImageFixtures;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The directly callable parts of UIContainerMissionSelect (Game.Runtime 0x020008e8): GetIsNewMission 0x642C80,
    // GetUIShowTypeForMission 0x643650, UpdateRankVisibility 0x643690, SetupLeaderboards 0x643BF0, TryNavigateToWidget
    // 0x642C00, OnContainerClosed 0x642B30 and OnDestroy 0x641A40. Not covered: Start, PopulateUI, InstantiateMissionToggle,
    // ToggleMission, SetMissionActive and RefreshZoneTheme, which need a level image load, the zone theme lookup, the mission
    // toggle prefab with its UIToggle and the toggle group, the App's storage and a UIManager; the leaderboard-available
    // branch of SetupLeaderboards (a ready leaderboard manager); and a UINavigationManager that exists.
    public sealed class UIContainerMissionSelectTests
    {
        private static object InvokeStatic(string name, params object[] arguments) =>
            typeof(UIContainerMissionSelect).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, arguments);

        // The container's field initialisers cache the navigation and UI manager references.
        private static UIContainerMissionSelect NewContainer(Owned owned)
        {
            owned.LeaseRow<UINavigationManager>();
            owned.LeaseRow<UIManager>();
            return owned.Host("Lucid mission select", false).AddComponent<UIContainerMissionSelect>();
        }

        private static MissionGroup NewGroup(params MissionDefinition[] definitions)
        {
            var group = new MissionGroup();
            Set(group, "m_definitions", new List<MissionDefinition>(definitions)); // Reflection: authored list
            return group;
        }

        private static MissionDefinition NewMission(Owned owned, string guid, string json = null)
        {
            var mission = Create<MissionDefinition>(owned, json);
            Set(mission, "m_guid", guid); // Reflection: authored GUID of a serialized definition
            Set(mission, "m_subMissions", new List<MissionDefinition>()); // Reflection: authored (empty) list
            return mission;
        }

        // GetIsNewMission 0x642C80: the first group holding the mission (by GUID) decides, returning the saved record's state; a
        // mission in no group gives the default (None) whatever the record says.
        [Test]
        public void ASavedNewStateIsReturnedOnlyForAMissionSomeGroupHolds()
        {
            WithWorld(world =>
            {
                Owned owned = world.Owned;
                MissionDefinition mission = NewMission(owned, "lucid-select-mission");
                MissionDefinition other = NewMission(owned, "lucid-select-other");
                var record = new SaveDataLevelMission("lucid-select-mission") { IsNewState = MissionIsNewState.IsNew };

                Assert.That(InvokeStatic("GetIsNewMission", new[] { NewGroup(other), NewGroup(other, mission) }, record), Is.EqualTo(MissionIsNewState.IsNew),
                    "found in the second group");
                record.IsNewState = MissionIsNewState.Seen;
                Assert.That(InvokeStatic("GetIsNewMission", new[] { NewGroup(mission), NewGroup(other) }, record), Is.EqualTo(MissionIsNewState.Seen));
                Assert.That(InvokeStatic("GetIsNewMission", new[] { NewGroup(other) }, record), Is.EqualTo(MissionIsNewState.None), "in no group");
                Assert.That(InvokeStatic("GetIsNewMission", new MissionGroup[0], record), Is.EqualTo(MissionIsNewState.None), "no groups");
            });
        }

        // GetUIShowTypeForMission 0x643650: a completed mission of the current level is unlocked, any other locked.
        [Test]
        public void AMissionShowsUnlockedOnlyOnceItIsComplete()
        {
            OriginalPersistentLevelObjectTests.WithMissions(true, (owned, missions) =>
            {
                owned.LeaseRow<TimeTrialLeaderboardManager>(); // MissionDefinition's field initialiser
                MissionDefinition mission = NewMission(owned, "lucid-select-mission");
                GameplayLevelDefinition level = missions.CurrentLevel.LevelDefinition;
                var container = NewContainer(owned);
                Set(container, "m_missionManager", missions.Manager); // Reflection: assigned by Start and OnMissionManagerValid
                Set(container, "m_metaGameState", new MetaGameState(level, null)); // Reflection: assigned by Start
                SaveDataLevelMission record = missions.Save.CurrentSave.GetOrCreateLevelData(level.GetGUID()).GetOrCreateMissionData(mission.GetGUID());

                Assert.That(Call(container, "GetUIShowTypeForMission", mission), Is.EqualTo(UIShowType.Locked));
                record.Complete = true;
                Assert.That(Call(container, "GetUIShowTypeForMission", mission), Is.EqualTo(UIShowType.Unlocked));
            });
        }

        private static void AddRank(Owned owned, DataManager data, RankType type, out Sprite[] icons)
        {
            RankDefinition definition = Create<RankDefinition>(owned, "{\"m_type\":" + (int)type + "}");
            Call(definition, "OnEnable"); // the wrappers Unity's own OnEnable builds, built again so they are certain to exist
            icons = new[] { NewSprite(owned), NewSprite(owned), NewSprite(owned) };
            Cache(definition.BaseAddressable, icons[0]);
            Cache(definition.BackgroundAddressable, icons[1]);
            Cache(definition.ForegroundAddressable, icons[2]);
            data.RankDefinitions[type] = definition;
        }

        // UpdateRankVisibility 0x643690 shows the rank icons of the rank the best time earns (TryGetRankForTimeSeconds, here S 30 s and
        // A 45 s above a theoretical best of 20 s) with the base, background and foreground in their own slots, and hides and
        // clears them when no rank is earned: a best time below the theoretical best, or past every rank with no D rank.
        [Test]
        public void TheRankIconsFollowTheRankTheBestTimeEarns()
        {
            WithWorld(world =>
            {
                Owned owned = world.Owned;
                var data = owned.Register(owned.Host("Lucid data manager", false).AddComponent<DataManager>());
                AddRank(owned, data, RankType.A, out Sprite[] aIcons);
                var ranks = owned.Track(ScriptableObject.CreateInstance<MissionRankDefinition>());
                foreach ((RankType type, int seconds) in new[] { (RankType.S, 30), (RankType.A, 45) })
                {
                    var rank = new MissionRank();
                    JsonUtility.FromJsonOverwrite("{\"m_rankType\":" + (int)type + ",\"m_seconds\":" + seconds + "}", rank);
                    rank.Validate();
                    ranks.Ranks.Add(type, rank);
                }
                Call(ranks, "OnEnable"); // the sorted list built from the ranks just added
                MissionDefinition mission = NewMission(owned, "lucid-select-mission", "{\"TheoreticalBestTimePossibleSeconds\":20}");
                Set(mission, "m_ranks", ranks); // Reflection: serialized reference
                var widget = owned.Host("Lucid mission toggle", false).AddComponent<UIWidgetToggleMission>();

                InvokeStatic("UpdateRankVisibility", mission, widget, 40f);
                Assert.That(widget.ShowRankIcon.Value, Is.True);
                Assert.That(new[] { widget.RankIconBase.Value, widget.RankIconBackground.Value, widget.RankIconForeground.Value }, Is.EqualTo(aIcons));

                foreach (float bestTime in new[] { 10f, 50f })
                {
                    InvokeStatic("UpdateRankVisibility", mission, widget, 40f);
                    InvokeStatic("UpdateRankVisibility", mission, widget, bestTime);
                    Assert.That(widget.ShowRankIcon.Value, Is.False, "best time " + bestTime);
                    Assert.That(widget.RankIconBase.Value, Is.Null, "best time " + bestTime);
                    Assert.That(widget.RankIconBackground.Value, Is.Null);
                    Assert.That(widget.RankIconForeground.Value, Is.Null);
                }
            });
        }

        // A mission with no rank definition hides and clears the icons too (the lookup fails at once).
        [Test]
        public void AMissionWithoutRanksHidesTheRankIcons()
        {
            WithWorld(world =>
            {
                Owned owned = world.Owned;
                var data = owned.Register(owned.Host("Lucid data manager", false).AddComponent<DataManager>());
                MissionDefinition mission = NewMission(owned, "lucid-select-mission");
                var widget = owned.Host("Lucid mission toggle", false).AddComponent<UIWidgetToggleMission>();
                widget.RankIconBase.Value = NewSprite(owned);
                widget.SetRankIconVisibility(true);

                InvokeStatic("UpdateRankVisibility", mission, widget, 40f);

                Assert.That(widget.ShowRankIcon.Value, Is.False);
                Assert.That(widget.RankIconBase.Value, Is.Null);
            });
        }

        // SetupLeaderboards 0x643BF0: a mission with no leaderboard identifier shows no leaderboards and leaves the identifier alone.
        [Test]
        public void AMissionWithoutALeaderboardShowsNoLeaderboards()
        {
            OriginalPersistentLevelObjectTests.WithMissions(true, (owned, missions) =>
            {
                owned.LeaseRow<TimeTrialLeaderboardManager>(); // MissionDefinition's field initialiser
                MissionDefinition mission = NewMission(owned, "lucid-select-mission");
                var container = NewContainer(owned);
                int identifierWrites = 0;
                container.ShowLeaderboards.Value = true;
                container.LeaderboardIdentifier.AddListener(() => identifierWrites++);

                Call(container, "SetupLeaderboards", mission);

                Assert.That(container.ShowLeaderboards.Value, Is.False);
                Assert.That(identifierWrites, Is.EqualTo(0));
            });
        }

        // TryNavigateToWidget 0x642C00 returns when the navigation manager row has no system; so does OnContainerClosed 0x642B30
        // (via the same test) and, with no active widget, before it.
        [Test]
        public void WithoutANavigationManagerOrAnActiveWidgetNavigationIsSkipped()
        {
            Owned.Run(owned =>
            {
                var container = NewContainer(owned);
                owned.LeaseRow<DataManager>(); // the widget's field initialiser
                var widget = owned.Host("Lucid mission toggle", false).AddComponent<UIWidgetToggleMission>();

                Assert.DoesNotThrow(() => Call(container, "TryNavigateToWidget", widget));
                Assert.DoesNotThrow(() => Call(container, "OnContainerClosed", new object[] { null }));
                Set(container, "m_activeWidget", widget); // Reflection: assigned by ToggleMission
                Assert.DoesNotThrow(() => Call(container, "OnContainerClosed", new object[] { null }));
            });
        }

        // OnDestroy 0x641A40 unloads the zone accent the container loaded.
        [Test]
        public void DestroyingTheContainerUnloadsItsZoneAccent()
        {
            Owned.Run(owned =>
            {
                var container = NewContainer(owned);
                ManagedAddressableAsset<Sprite> accent = LoadedWrapper(owned, out Sprite sprite);
                Assert.That(accent.Load(), Is.SameAs(sprite));
                Assert.That(Count(accent), Is.EqualTo(1));
                Set(container, "m_zoneAccent", accent); // Reflection: assigned by RefreshZoneTheme

                Call(container, "OnDestroy");

                Assert.That(Count(accent), Is.EqualTo(0));
                Assert.That(IsCached(accent), Is.False);
            });
        }
    }
}
