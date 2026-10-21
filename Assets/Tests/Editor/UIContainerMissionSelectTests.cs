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
    // 0x642C00, OnContainerClosed 0x642B30 and OnDestroy 0x641A40, plus InstantiateMissionToggle 0x642F50 over a real
    // mission toggle prefab (a UIWidgetToggleMission with its toggle and group element, a real DataManager whose reward
    // collectable holds its sprite) and the ghost and index handling of PopulateUI 0x642180 for groups whose missions are
    // all complete. Not covered: Start, ToggleMission, SetMissionActive and RefreshZoneTheme, which need the App's storage
    // (SetMissionActive stores the meta game state there), a level image load, the zone theme lookup, the results and
    // active-mission containers and a UIManager; so also the part of PopulateUI that picks the selection (an incomplete
    // mission whose archetype requirement is met becomes the active mission while nothing is selected, and only then does
    // the meta game state's mission, 0x6429F7), because choosing it reaches SetMissionActive and selecting a widget reaches
    // ToggleMission; the leaderboard-available branch of SetupLeaderboards (a ready leaderboard manager); and a
    // UINavigationManager that exists.
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

        private sealed class ToggleRig
        {
            internal Owned Owned;
            internal OriginalPersistentLevelObjectTests.Missions Missions;
            internal UIContainerMissionSelect Container;
            internal UIToggleGroup Group;
            internal GameplayLevelDefinition Level;
        }

        // A container over a real mission manager and save, with a mission toggle prefab (an inactive host) as m_missionPrefab and
        // an inactive host with a toggle group as m_missionsRoot. The collectable counter and the ghost prefab are filled too.
        private static void WithToggles(Action<ToggleRig> body) => OriginalPersistentLevelObjectTests.WithMissions(true, (owned, missions) =>
        {
            owned.LeaseRow<TimeTrialLeaderboardManager>(); // MissionDefinition's field initialiser
            var data = owned.Register(owned.Host("Lucid data manager", false).AddComponent<DataManager>()); // the widgets' field initialiser
            var reward = owned.Track(ScriptableObject.CreateInstance<CollectableDefinition>());
            Set(reward, "m_loadedSprite", NewSprite(owned)); // Reflection: the sprite an earlier Addressables load would hold
            data.CollectableDefinitions[CollectableType.Orb] = reward;
            var rig = new ToggleRig { Owned = owned, Missions = missions, Level = missions.CurrentLevel.LevelDefinition };
            rig.Container = NewContainer(owned);
            rig.Group = owned.Host("Lucid mission toggle group", false).AddComponent<UIToggleGroup>();
            GameObject prefabHost = owned.Host("Lucid mission toggle", false);
            var widget = prefabHost.AddComponent<UIWidgetToggleMission>();
            var toggle = prefabHost.GetComponent<UIToggle>();
            var element = prefabHost.AddComponent<UIToggleGroupElement>();
            Set(toggle, "m_groupElement", element); // Reflection: serialized references the editor fills in
            Set(element, "m_toggle", toggle);
            Set(widget, "m_toggle", toggle);
            Set(rig.Container, "m_missionPrefab", widget); // Reflection: serialized references
            Set(rig.Container, "m_missionsRoot", rig.Group);
            Set(rig.Container, "m_ghostedMissionPrefab", owned.Host("Lucid ghosted mission", false));
            Set(rig.Container, "m_collectableCounter", owned.Host("Lucid collectable counter", false).AddComponent<UIContainerOrbCollectableCounter>());
            Set(rig.Container, "m_missionManager", missions.Manager); // Reflection: assigned by Start and OnMissionManagerValid
            Set(rig.Container, "m_saveManager", missions.Save);
            Set(rig.Container, "m_metaGameState", new MetaGameState(rig.Level, null)); // Reflection: assigned by Start
            body(rig);
        });

        private static void SetComplete(ToggleRig rig, MissionDefinition mission, bool complete) =>
            rig.Missions.Save.CurrentSave.GetOrCreateLevelData(rig.Level.GetGUID()).GetOrCreateMissionData(mission.GetGUID()).Complete = complete;

        // A sub-mission of the given type in the given completion state.
        private static MissionDefinition NewSubMission(ToggleRig rig, string guid, MissionType type, bool complete)
        {
            MissionDefinition subMission = NewMission(rig.Owned, guid, "{\"m_type\":" + (int)type + "}");
            SetComplete(rig, subMission, complete);
            return subMission;
        }

        // The widget InstantiateMissionToggle 0x642F50 makes for a mission with the given sub-missions (none of them locked itself).
        private static UIWidgetToggleMission MakeToggle(ToggleRig rig, int index, params MissionDefinition[] subMissions)
        {
            MissionDefinition mission = NewMission(rig.Owned, "lucid-select-parent-" + index);
            Set(mission, "m_subMissions", new List<MissionDefinition>(subMissions)); // Reflection: authored list
            return (UIWidgetToggleMission)Call(rig.Container, "InstantiateMissionToggle", index, mission, false, false, true, MissionIsNewState.None, 0f);
        }

        // 0x642F50 builds the sub-reward states from the sub-missions by type. The jukebox state is Locked (0xE5D5F0E8) once a
        // Locked one has been seen (cmp 0xE5D5F0E8; cmovne at 0x64320C), so the order of the sub-missions does not matter.
        [Test]
        public void TheJukeboxStaysLockedOnceASubMissionIsLocked()
        {
            WithToggles(rig =>
            {
                MissionDefinition locked = NewSubMission(rig, "lucid-jukebox-locked", MissionType.Jukebox, false);
                MissionDefinition unlocked = NewSubMission(rig, "lucid-jukebox-unlocked", MissionType.Jukebox, true);

                Assert.That(MakeToggle(rig, 1, locked, unlocked).JukeboxShowState.Value, Is.EqualTo(UIShowType.Locked), "locked then unlocked");
                Assert.That(MakeToggle(rig, 2, unlocked, locked).JukeboxShowState.Value, Is.EqualTo(UIShowType.Locked), "unlocked then locked");
                Assert.That(MakeToggle(rig, 3, unlocked, unlocked).JukeboxShowState.Value, Is.EqualTo(UIShowType.Unlocked), "all unlocked");
            });
        }

        // 0x642F50 stores red star rings and blue coins plainly (0x6431CC, 0x643255): the last sub-mission of the type decides,
        // and a sub-mission of another type does not disturb it.
        [Test]
        public void RedStarRingsAndBlueCoinsFollowTheLastSubMissionOfTheirType()
        {
            WithToggles(rig =>
            {
                MissionDefinition redLocked = NewSubMission(rig, "lucid-red-locked", MissionType.RedStarRings, false);
                MissionDefinition redUnlocked = NewSubMission(rig, "lucid-red-unlocked", MissionType.RedStarRings, true);
                MissionDefinition blueLocked = NewSubMission(rig, "lucid-blue-locked", MissionType.BlueCoins, false);
                MissionDefinition blueUnlocked = NewSubMission(rig, "lucid-blue-unlocked", MissionType.BlueCoins, true);
                MissionDefinition other = NewSubMission(rig, "lucid-default-locked", MissionType.Default, false);

                UIWidgetToggleMission first = MakeToggle(rig, 1, redLocked, blueUnlocked, redUnlocked, other, blueLocked);
                Assert.That(first.RedStarRingsShowState.Value, Is.EqualTo(UIShowType.Unlocked), "the last red star rings mission is complete");
                Assert.That(first.BlueCoinsShowState.Value, Is.EqualTo(UIShowType.Locked), "the last blue coins mission is not");

                UIWidgetToggleMission second = MakeToggle(rig, 2, redUnlocked, blueLocked, redLocked, other, blueUnlocked);
                Assert.That(second.RedStarRingsShowState.Value, Is.EqualTo(UIShowType.Locked));
                Assert.That(second.BlueCoinsShowState.Value, Is.EqualTo(UIShowType.Unlocked));
                Assert.That(second.JukeboxShowState.Value, Is.EqualTo(UIShowType.Hidden), "no jukebox sub-mission");
            });
        }

        // Without sub-missions every sub-reward state keeps its initial Hidden (0x875AD1C4).
        [Test]
        public void WithoutSubMissionsTheSubRewardsAreHidden()
        {
            WithToggles(rig =>
            {
                UIWidgetToggleMission widget = MakeToggle(rig, 1);

                Assert.That(widget.RedStarRingsShowState.Value, Is.EqualTo(UIShowType.Hidden));
                Assert.That(widget.BlueCoinsShowState.Value, Is.EqualTo(UIShowType.Hidden));
                Assert.That(widget.JukeboxShowState.Value, Is.EqualTo(UIShowType.Hidden));
            });
        }

        // The label is the index in decimal, and the widget is put in the container's toggle group.
        [Test]
        public void TheToggleCarriesItsIndexLabelAndJoinsTheToggleGroup()
        {
            WithToggles(rig =>
            {
                UIWidgetToggleMission widget = MakeToggle(rig, 7);

                Assert.That(widget.Label.Value, Is.EqualTo("7"));
                Assert.That(widget.GetComponent<UIToggle>().GroupElement.Group, Is.SameAs(rig.Group));
                Assert.That(widget.transform.parent, Is.SameAs(rig.Group.transform));
                Assert.That(widget, Is.Not.SameAs(Get<UIWidgetToggleMission>(rig.Container, "m_missionPrefab")), "a clone of the prefab");
            });
        }

        private static MissionGroup NewRequirementGroup(bool ghosted, RequirementMissionGroupBase requirement, params MissionDefinition[] definitions)
        {
            MissionGroup group = NewGroup(definitions);
            Set(group, "m_requirements", requirement == null ? new List<RequirementMissionGroupBase>() : new List<RequirementMissionGroupBase> { requirement }); // Reflection: authored list
            Set(group, "m_showAsGhostedWhenRequirementsNotMet", ghosted);
            return group;
        }

        // PopulateUI 0x642180: a group with an always-hidden requirement is skipped, a toggle is made for each mission of a group
        // whose requirements are met (counting up the index), and for an unmet group a ghost is made for each of its missions when
        // it shows as ghosted (0x6426A4..0x642516), without counting. The children therefore come out in group order.
        [Test]
        public void GhostsAreMadeForEachMissionOfAnUnmetGhostedGroupAndOnlyMetMissionsAreCounted()
        {
            WithToggles(rig =>
            {
                Owned owned = rig.Owned;
                owned.LeaseRow<ChallengeManager>(); // the unmet requirement's field initialiser: no manager, so not met
                MissionDefinition hidden = NewMission(owned, "lucid-populate-hidden");
                MissionDefinition first = NewMission(owned, "lucid-populate-first");
                MissionDefinition second = NewMission(owned, "lucid-populate-second");
                MissionDefinition ghostA = NewMission(owned, "lucid-populate-ghost-a");
                MissionDefinition ghostB = NewMission(owned, "lucid-populate-ghost-b");
                MissionDefinition unshown = NewMission(owned, "lucid-populate-unshown");
                MissionDefinition third = NewMission(owned, "lucid-populate-third");
                SaveDataLevel saveLevel = rig.Missions.Save.CurrentSave.GetOrCreateLevelData(rig.Level.GetGUID());
                foreach (MissionDefinition mission in new[] { hidden, first, second, ghostA, ghostB, unshown, third })
                {
                    SetComplete(rig, mission, true);
                    Call(rig.Missions.Manager, "CreateMissionState", mission, saveLevel);
                }
                var unmet = Create<RequirementMissionGroupShadowFTUEAvailable>(owned);
                var groups = new MissionGroup[]
                {
                    NewRequirementGroup(true, Create<RequirementMissionGroupAlwaysHidden>(owned), hidden),
                    NewRequirementGroup(false, null, first, second),
                    NewRequirementGroup(true, unmet, ghostA, ghostB),
                    NewRequirementGroup(false, unmet, unshown),
                    NewRequirementGroup(false, null, third),
                };

                Call(rig.Container, "PopulateUI", new List<MissionGroup>(groups));

                var children = new List<string>();
                foreach (Transform child in rig.Group.transform)
                    children.Add(child.name + (child.GetComponent<UIWidgetToggleMission>() != null ? ":" + child.GetComponent<UIWidgetToggleMission>().Label.Value : ""));
                Assert.That(children, Is.EqualTo(new[]
                {
                    "Lucid mission toggle(Clone):1", "Lucid mission toggle(Clone):2",
                    "Lucid ghosted mission(Clone)", "Lucid ghosted mission(Clone)",
                    "Lucid mission toggle(Clone):3",
                }));
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
        // (via the same test). The null-widget early return is not observable without a navigation manager recording its calls, so only the absence of a throw is checked.
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
