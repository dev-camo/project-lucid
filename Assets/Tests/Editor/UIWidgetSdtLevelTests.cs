using System;
using System.Collections.Generic;
using System.Linq;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.MissionTrackerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetSDTLevel (Game.Runtime 0x020009c4, x86-64 0x69FD70..0x6A1E53) as a real component on an inactive owned host over a real
    // MissionManager, DataManager (whose reward collectable already holds its loaded sprite) and an owned string table.
    // Not covered: OnEnable (a coroutine cannot start on an inactive host), the unlock and boss preview animations (Unlock 0x6A0AD0,
    // HideBoss 0x6A0A40: they play clips on the toggle's UITransitionAnimation), the navigation manager selection (TryNavigateToWidget 0x6A17F0 and its inlined copies at 0x6A16FD / 0x6A179A / 0x6A1E08) and the
    // locking requirement messages (0x6A02B9..0x6A0549), which need level requirements with a save and a progression. The fixture
    // gives the toggle's transition an empty settings list so that queuing a state transition is a no-op.
    public sealed class UIWidgetSdtLevelTests
    {
        private const string FormatXOfY = "of", PlayPrevious = "play previous", NeedRewards = "need rewards", DisplayName = "Level display name";

        private sealed class Scene
        {
            public UIWidgetSDTLevel Widget;
            public UIToggle Toggle;
            public UIWidgetNavigation Navigation;
            public Image Fill;
            public CanvasGroup Group;
            public CollectableDefinition Reward;
            public Sprite RewardIcon;
            public GameplayLevelDefinition Level;
            public ProgressionCollectableLevel Progress;
            public Dictionary<CollectableType, ProgressionCollectable> Overall;
            public int HiddenRaised;

            public List<UIContainerRewardRestriction> Instances => Get<List<UIContainerRewardRestriction>>(Widget, "m_rewardRestrictionInstances");

            public UIWidgetSDTLevelParameters Parameters(bool isNext = false, bool locked = false, bool unlockSeen = true, bool isNew = false,
                int number = 7, bool hidden = false) =>
                new UIWidgetSDTLevelParameters(Level, Progress, Overall, isNext, locked, unlockSeen, isNew, number, hidden);
        }

        private static string LevelJson(bool boss = false, bool alwaysUnlocked = false, string rewardRequirements = "") =>
            "{\"m_displayName\":" + (int)Strings.CULTURE_NUMBER_MILLIONS + ",\"m_requirements\":[],\"m_isBossLevel\":" + (boss ? "true" : "false") +
            ",\"m_alwaysUnlocked\":" + (alwaysUnlocked ? "true" : "false") + ",\"m_rewardRequirements\":[" + rewardRequirements + "]}";

        private static string Requirement(int count) => "{\"RewardType\":" + (int)CollectableType.Orb + ",\"Count\":" + count + "}";

        private static void Run(Action<Owned, Scene> body, string levelJson = null, bool withMissionList = true) => WithWorld(world =>
            OwnedStringTable.Run(new Dictionary<Strings, string>
            {
                { Strings.FORMAT_X_OF_Y, FormatXOfY },
                { Strings.LEVEL_SELECT_PLAY_PREVIOUS_LEVEL, PlayPrevious },
                { Strings.MENU_ACT_REWARD_REQUIREMENT, NeedRewards },
                { Strings.CULTURE_NUMBER_MILLIONS, DisplayName },
            }, () =>
            {
                Owned owned = world.Owned;
                var scene = new Scene();
                // The widget caches its UINavigationManager and DataManager references when it is constructed.
                owned.LeaseRow<UINavigationManager>();
                DataManager data = owned.Host("Lucid sdt level data manager", false).AddComponent<DataManager>();
                owned.Register(data);
                scene.Reward = owned.Track(ScriptableObject.CreateInstance<CollectableDefinition>());
                scene.RewardIcon = NewSprite(owned);
                Set(scene.Reward, "m_loadedSprite", scene.RewardIcon); // Reflection: the sprite an earlier Addressables load would hold
                data.CollectableDefinitions[CollectableType.Orb] = scene.Reward;

                scene.Level = Create<GameplayLevelDefinition>(owned, levelJson ?? LevelJson());
                if (withMissionList)
                {
                    var missions = Create<MissionList>(owned);
                    Set(missions, "m_groups", new List<MissionGroup>()); // Reflection: authored (empty) mission list
                    Set(scene.Level, "m_missionList", missions); // Reflection: serialized reference
                }
                scene.Progress = new ProgressionCollectableLevel(scene.Level);
                scene.Overall = new Dictionary<CollectableType, ProgressionCollectable>();

                GameObject host = owned.Host("Lucid sdt level", false);
                scene.Widget = host.AddComponent<UIWidgetSDTLevel>();
                scene.Toggle = host.AddComponent<UIToggle>();
                scene.Navigation = host.AddComponent<UIWidgetNavigation>();
                scene.Fill = host.AddComponent<Image>();
                scene.Group = host.AddComponent<CanvasGroup>();
                var transition = host.AddComponent<UITransitionAnimation>();
                Set(transition, "m_settings", new List<UITransitionAnimationSetting>()); // Reflection: authored (empty) transition settings
                Set(scene.Toggle, "m_transition", transition); // Reflection: serialized reference
                Set(scene.Widget, "m_toggle", scene.Toggle); // Reflection: serialized references
                Set(scene.Widget, "m_uiWidgetNavigation", scene.Navigation);
                Set(scene.Widget, "m_fillImage", scene.Fill);
                Set(scene.Widget, "m_canvasGroup", scene.Group);
                var hidden = new UnityEvent();
                hidden.AddListener(() => scene.HiddenRaised++);
                Set(scene.Widget, "m_onLevelHidden", hidden);
                GameObject requirementsParent = owned.Host("Lucid sdt level requirements", false);
                Set(scene.Widget, "m_rewardRequirementsParent", requirementsParent.transform);
                Set(scene.Widget, "m_rewardRequirementUIContainerPrefab",
                    owned.Host("Lucid sdt level requirement prefab", false).AddComponent<UIContainerRewardRestriction>());
                body(owned, scene);
            }));

        private static void SetProgress(Scene scene, int collected, int total)
        {
            // Reflection: the dictionaries are readonly fields the progression fills from save data.
            Get<Dictionary<CollectableType, ProgressionCollectable>>(scene.Progress, "m_progressGroupsIgnored")[CollectableType.Orb] =
                new ProgressionCollectable(CollectableType.Orb, collected, total);
        }

        // Setup 0x69FF30 (base setup, the mission manager, the parameters, the format string) and UpdateBinders 0x6A0050: the name is the
        // numerical name while m_useNumericalName holds (0x6A00C4), the orb counts and the fill come from the progress that ignores groups
        // (0x6A0175, +0x20 of the progress object), not the main one, and with no unmet requirement the message is the play-previous text.
        [Test]
        public void SetupPublishesTheNumberTheGroupIgnoredProgressAndTheDefaultMessage()
        {
            Run((owned, scene) =>
            {
                SetProgress(scene, 3, 4);
                Get<Dictionary<CollectableType, ProgressionCollectable>>(scene.Progress, "m_progress")[CollectableType.Orb] =
                    new ProgressionCollectable(CollectableType.Orb, 9, 9); // the decoy that must not be read

                scene.Widget.Setup(scene.Parameters(isNext: true, isNew: true, number: 7));

                Assert.That(scene.Widget.FormatXOfY.Value, Is.EqualTo(FormatXOfY));
                Assert.That(scene.Widget.LevelName.Value, Is.EqualTo("7"));
                Assert.That(scene.Widget.IsNextLevel.Value, Is.True);
                Assert.That(scene.Fill.fillAmount, Is.EqualTo(0.75f));
                Assert.That(scene.Widget.LevelOrbsCollected.Value, Is.EqualTo(3));
                Assert.That(scene.Widget.LevelTotalOrbs.Value, Is.EqualTo(4));
                Assert.That(scene.Widget.ShowRewardsLeftToUnlockMessage.Value, Is.False);
                Assert.That(scene.Widget.RewardsUnlockMessage.Value, Is.EqualTo(PlayPrevious));
                Assert.That(scene.Widget.RewardIcon.Value, Is.SameAs(scene.RewardIcon));
                Assert.That(scene.Widget.IsNew.Value, Is.True);
                Assert.That(scene.Widget.IsLocked.Value, Is.False);
            });
        }

        [Test]
        public void SetupPublishesTheDisplayNameWhenNumericalNamesAreOff()
        {
            Run((owned, scene) =>
            {
                Set(scene.Widget, "m_useNumericalName", false); // Reflection: serialized option
                SetProgress(scene, 0, 1);
                scene.Widget.Setup(scene.Parameters(number: 7));
                Assert.That(scene.Widget.LevelName.Value, Is.EqualTo(DisplayName));
            });
        }

        // 0x6A03D9 / 0x6A0F3B: a locked level answers Hidden for the three mission types and the rank without asking the mission manager;
        // the level here has no mission list, so asking would fault.
        [Test]
        public void ALockedLevelHidesEveryRewardStateWithoutAskingTheMissionManager()
        {
            Run((owned, scene) =>
            {
                SetProgress(scene, 0, 1);
                scene.Widget.Setup(scene.Parameters(locked: true));

                Assert.That(scene.Widget.IsLocked.Value, Is.True);
                Assert.That(scene.Widget.IsNew.Value, Is.False);
                Assert.That(scene.Widget.RedStarRingsShowState.Value, Is.EqualTo(UIShowType.Hidden));
                Assert.That(scene.Widget.BlueCoinsShowState.Value, Is.EqualTo(UIShowType.Hidden));
                Assert.That(scene.Widget.JukeboxShowState.Value, Is.EqualTo(UIShowType.Hidden));
                Assert.That(scene.Widget.RankShowState.Value, Is.EqualTo(UIShowType.Hidden));
            }, withMissionList: false);
        }

        // ShowRewardRequirementUI 0x6A0C50 through UpdateBinders: an unmet reward requirement shows the requirement message and one
        // restriction container with the remaining count (the requirement's count minus the overall collected, 0x6A0DFE).
        [Test]
        public void AnUnmetRewardRequirementShowsOneRestrictionWithTheRemainingCount()
        {
            Run((owned, scene) =>
            {
                SetProgress(scene, 0, 1);
                scene.Overall[CollectableType.Orb] = new ProgressionCollectable(CollectableType.Orb, 4, 100);

                scene.Widget.Setup(scene.Parameters());

                Assert.That(scene.Widget.ShowRewardsLeftToUnlockMessage.Value, Is.True);
                Assert.That(scene.Widget.RewardsUnlockMessage.Value, Is.EqualTo(NeedRewards));
                Assert.That(scene.Instances, Has.Count.EqualTo(1));
                Assert.That(scene.Instances[0].Count.Value, Is.EqualTo(6));
                Assert.That(scene.Instances[0].Icon.Value, Is.SameAs(scene.RewardIcon));
            }, LevelJson(rewardRequirements: Requirement(10)));
        }

        // 0x6A0E0A..0x6A0E15: a container is created only when the list is shorter than the next index, so a second pass reuses the
        // first container and publishes the new remaining count on it.
        [Test]
        public void RefreshingReusesTheRestrictionContainerAndUpdatesItsCount()
        {
            Run((owned, scene) =>
            {
                SetProgress(scene, 0, 1);
                scene.Overall[CollectableType.Orb] = new ProgressionCollectable(CollectableType.Orb, 4, 100);
                scene.Widget.Setup(scene.Parameters());
                UIContainerRewardRestriction first = scene.Instances[0];

                scene.Overall[CollectableType.Orb] = new ProgressionCollectable(CollectableType.Orb, 7, 100);
                Assert.That(Call(scene.Widget, "ShowRewardRequirementUI"), Is.True);

                Assert.That(scene.Instances, Has.Count.EqualTo(1));
                Assert.That(scene.Instances[0], Is.SameAs(first));
                Assert.That(first.Count.Value, Is.EqualTo(3));
            }, LevelJson(rewardRequirements: Requirement(10)));
        }

        // One container per unmet requirement, in authored order, and a requirement that is already met (remaining <= 0, 0x6A0E08) does
        // not consume a container index.
        [Test]
        public void EachUnmetRequirementGetsItsOwnContainerAndMetOnesAreSkipped()
        {
            Run((owned, scene) =>
            {
                SetProgress(scene, 0, 1);
                scene.Overall[CollectableType.Orb] = new ProgressionCollectable(CollectableType.Orb, 4, 100);

                scene.Widget.Setup(scene.Parameters());

                Assert.That(scene.Instances.Select(instance => instance.Count.Value), Is.EqualTo(new[] { 6, 16 }));
            }, LevelJson(rewardRequirements: Requirement(3) + "," + Requirement(10) + "," + Requirement(20)));
        }

        // With every requirement met (remaining <= 0 at 0x6A0E08) no container is made and the answer is false.
        [Test]
        public void MetRequirementsShowNoRestriction()
        {
            Run((owned, scene) =>
            {
                SetProgress(scene, 0, 1);
                scene.Overall[CollectableType.Orb] = new ProgressionCollectable(CollectableType.Orb, 10, 100);
                scene.Widget.Setup(scene.Parameters());
                Assert.That(Call(scene.Widget, "ShowRewardRequirementUI"), Is.False);
                Assert.That(scene.Instances, Is.Empty);
                Assert.That(scene.Widget.ShowRewardsLeftToUnlockMessage.Value, Is.False);
            }, LevelJson(rewardRequirements: Requirement(10)));
        }

        // UpdateBinders 0x6A0050 combines the two answers without short-circuiting: HasRequirement<RequirementLevelAlwaysLocked> is called
        // at 0x6A0297, ShowRewardRequirementUI unconditionally at 0x6A0549, and the results are or-ed (0x6A0551) before the message is
        // chosen (cmovne at 0x6A0569). An always-locked level with an unmet reward requirement still builds its restriction containers
        // (rejects a short-circuiting ||); with every requirement met it shows the requirement message and no containers (rejects
        // ignoring the always-locked requirement). Only the type test over the requirement list is involved, so no save is needed.
        [TestCase(4, 1, 6, TestName = "AnAlwaysLockedLevelStillBuildsItsUnmetRestrictions")]
        [TestCase(10, 0, 0, TestName = "AnAlwaysLockedLevelWithMetRequirementsShowsNoRestriction")]
        public void AnAlwaysLockedLevelShowsTheRequirementMessageAndStillRefreshesItsRestrictions(int overallCollected, int containers, int remaining)
        {
            Run((owned, scene) =>
            {
                var alwaysLocked = Create<RequirementLevelAlwaysLocked>(owned);
                Set(scene.Level, "m_requirements", new List<RequirementGameplayLevelBase> { alwaysLocked }); // Reflection: serialized list
                SetProgress(scene, 0, 1);
                scene.Overall[CollectableType.Orb] = new ProgressionCollectable(CollectableType.Orb, overallCollected, 100);

                scene.Widget.Setup(scene.Parameters());

                Assert.That(scene.Widget.ShowRewardsLeftToUnlockMessage.Value, Is.True);
                Assert.That(scene.Widget.RewardsUnlockMessage.Value, Is.EqualTo(NeedRewards));
                Assert.That(scene.Instances, Has.Count.EqualTo(containers));
                if (containers > 0)
                    Assert.That(scene.Instances[0].Count.Value, Is.EqualTo(remaining));
            }, LevelJson(rewardRequirements: Requirement(10)));
        }

        // GetProgressionPercent 0x6A0B70: collected over total as single-precision floats from the progress that ignores groups, zero when
        // the collectable has no entry (the failed TryGetValue answer at 0x6A0C27).
        [TestCase(3, 4, 0.75f, TestName = "ThreeOfFourIsThreeQuarters")]
        [TestCase(1, 3, 0.33333334f, TestName = "OneOfThreeIsRoundedToSinglePrecision")]
        [TestCase(5, 4, 1.25f, TestName = "MoreCollectedThanTotalIsNotClamped")]
        [TestCase(0, 7, 0f, TestName = "NoneCollectedIsZero")]
        public void TheProgressionPercentIsCollectedOverTotal(int collected, int total, float expected)
        {
            Run((owned, scene) =>
            {
                SetProgress(scene, collected, total);
                Assert.That(Call(scene.Widget, "GetProgressionPercent", CollectableType.Orb, scene.Progress), Is.EqualTo(expected));
            });
        }

        [Test]
        public void TheProgressionPercentOfAnUntrackedCollectableIsZeroAndOfAnEmptyOneNotANumber()
        {
            Run((owned, scene) =>
            {
                Assert.That(Call(scene.Widget, "GetProgressionPercent", CollectableType.Orb, scene.Progress), Is.EqualTo(0f));
                SetProgress(scene, 0, 0);
                Assert.That((float)Call(scene.Widget, "GetProgressionPercent", CollectableType.Orb, scene.Progress), Is.NaN);
            });
        }

        // GetRewardRemainingCount 0x6A15F0: the requirement's count minus what the overall progress collected; an untracked
        // collectable counts as zero collected, and the result can go negative.
        [TestCase(4, true, 6, TestName = "FourCollectedLeavesSixRemaining")]
        [TestCase(0, false, 10, TestName = "AnUntrackedCollectableLeavesTheFullCount")]
        [TestCase(13, true, -3, TestName = "OverCollectingLeavesANegativeRemainder")]
        public void TheRemainingRewardCountIsTheRequirementMinusTheOverallCollected(int collected, bool tracked, int expected)
        {
            Run((owned, scene) =>
            {
                if (tracked) scene.Overall[CollectableType.Orb] = new ProgressionCollectable(CollectableType.Orb, collected, 100);
                SetProgress(scene, 0, 1);
                scene.Widget.Setup(scene.Parameters());
                var requirement = new RewardRequirement { RewardType = CollectableType.Orb, Count = 10 };
                Assert.That(Call(scene.Widget, "GetRewardRemainingCount", requirement), Is.EqualTo(expected));
            });
        }

        // GetCollectableDefinition 0x6A06E0: the data manager's definition for the reward type, but only with parameters that carry a
        // level progress and a reward type other than None (0x6A0718..0x6A0728); otherwise null.
        [Test]
        public void TheCollectableDefinitionNeedsParametersWithAProgressAndARewardType()
        {
            Run((owned, scene) =>
            {
                Assert.That(Call(scene.Widget, "GetCollectableDefinition"), Is.Null, "no parameters yet");

                // Reflection: UpdateBinders dereferences the level progress, so Setup cannot reach the parameters without one.
                Set(scene.Widget, "m_parameters", new UIWidgetSDTLevelParameters(scene.Level, null, scene.Overall, false, false, true, false, 1, false));
                Assert.That(Call(scene.Widget, "GetCollectableDefinition"), Is.Null, "no level progress");

                Set(scene.Widget, "m_parameters", scene.Parameters());
                Assert.That(Call(scene.Widget, "GetCollectableDefinition"), Is.SameAs(scene.Reward));

                Set(scene.Widget, "m_rewardType", CollectableType.None); // Reflection: serialized option
                Assert.That(Call(scene.Widget, "GetCollectableDefinition"), Is.Null, "no reward type");
            });
        }

        // OnDisable 0x6A0600 releases the reward icon UpdateBinders loaded, and does nothing before any parameters exist (the
        // definition lookup answers null and the Unity null test returns).
        [Test]
        public void DisablingReleasesTheRewardIconOnlyOnceItWasLoaded()
        {
            Run((owned, scene) =>
            {
                Assert.DoesNotThrow(() => Call(scene.Widget, "OnDisable"));
                Assert.That(Get<int>(scene.Reward, "m_refCount"), Is.EqualTo(0));

                SetProgress(scene, 0, 1);
                scene.Widget.Setup(scene.Parameters());
                Assert.That(Get<int>(scene.Reward, "m_refCount"), Is.EqualTo(1), "UpdateBinders loaded the icon");

                Call(scene.Widget, "OnDisable");
                Assert.That(Get<int>(scene.Reward, "m_refCount"), Is.EqualTo(0));
            });
        }

        // Start 0x69FD70 adds Refresh to the string table's change handlers and OnDestroy 0x69FE50 removes it again.
        [Test]
        public void TheWidgetFollowsTheStringTableChangesBetweenStartAndDestroy()
        {
            Run((owned, scene) =>
            {
                FastAction saved = StringTable.StringsTableHandlers;
                try
                {
                    Call(scene.Widget, "Start");
                    Assert.That(StringTable.StringsTableHandlers.GetInvocationList()
                        .Count(handler => ReferenceEquals(handler.Target, scene.Widget) && handler.Method.Name == "Refresh"), Is.EqualTo(1));

                    Call(scene.Widget, "OnDestroy");
                    Assert.That(StringTable.StringsTableHandlers == null || StringTable.StringsTableHandlers.GetInvocationList()
                        .All(handler => !ReferenceEquals(handler.Target, scene.Widget)), Is.True);
                }
                finally
                {
                    StringTable.StringsTableHandlers = saved;
                }
            });
        }

        // Refresh 0x6A0770: a hidden level is hidden (not interactable, navigation off, transparent, the hidden event raised); hidden wins
        // over locked.
        [TestCase(false, TestName = "RefreshHidesAHiddenLevel")]
        [TestCase(true, TestName = "RefreshHidesAHiddenLevelThatIsAlsoLocked")]
        public void RefreshHidesAHiddenLevel(bool alsoLocked)
        {
            Run((owned, scene) =>
            {
                SetProgress(scene, 0, 1);
                scene.Widget.Setup(scene.Parameters(hidden: true, locked: alsoLocked));
                Assert.That(scene.Group.alpha, Is.EqualTo(1f));

                Call(scene.Widget, "Refresh");

                Assert.That(scene.Toggle.Interactable, Is.False);
                Assert.That(scene.Navigation.enabled, Is.False);
                Assert.That(scene.Group.alpha, Is.EqualTo(0f));
                Assert.That(scene.HiddenRaised, Is.EqualTo(1));
            });
        }

        // A locked level (not hidden) only loses its interactivity and navigation: 0x6A07D4..0x6A0800 never touches the alpha or the event.
        [Test]
        public void RefreshLocksALockedLevelWithoutHidingIt()
        {
            Run((owned, scene) =>
            {
                SetProgress(scene, 0, 1);
                scene.Widget.Setup(scene.Parameters(locked: true));

                Call(scene.Widget, "Refresh");

                Assert.That(scene.Toggle.Interactable, Is.False);
                Assert.That(scene.Navigation.enabled, Is.False);
                Assert.That(scene.Group.alpha, Is.EqualTo(1f));
                Assert.That(scene.HiddenRaised, Is.EqualTo(0));
            }, withMissionList: false);
        }

        // An unlocked level whose unlock was seen, or one that is always unlocked, is left alone (0x6A087B: no unlock animation).
        [TestCase(true, false, TestName = "RefreshLeavesALevelWhoseUnlockWasSeenAlone")]
        [TestCase(false, true, TestName = "RefreshLeavesAnAlwaysUnlockedLevelAlone")]
        public void RefreshLeavesAnUnlockedLevelAlone(bool unlockSeen, bool alwaysUnlocked)
        {
            Run((owned, scene) =>
            {
                SetProgress(scene, 0, 1);
                scene.Widget.Setup(scene.Parameters(unlockSeen: unlockSeen));

                Assert.DoesNotThrow(() => Call(scene.Widget, "Refresh"));

                Assert.That(scene.Toggle.Interactable, Is.True);
                Assert.That(scene.Navigation.enabled, Is.True);
                Assert.That(scene.Group.alpha, Is.EqualTo(1f));
                Assert.That(scene.HiddenRaised, Is.EqualTo(0));
            }, LevelJson(alwaysUnlocked: alwaysUnlocked));
        }

        // IsHiddenBoss 0x6A09B0: only a boss level can be hidden, and a boss level without a mission list is hidden.
        [Test]
        public void OnlyABossLevelWithoutCompleteMissionsIsAHiddenBoss()
        {
            Run((owned, scene) =>
            {
                Set(scene.Widget, "m_parameters", scene.Parameters()); // Reflection: Setup would also build the bindings
                Assert.That(Call(scene.Widget, "IsHiddenBoss"), Is.False, "not a boss level");
            });
            Run((owned, scene) =>
            {
                Set(scene.Widget, "m_parameters", scene.Parameters()); // Reflection: Setup would also build the bindings
                Assert.That(Call(scene.Widget, "IsHiddenBoss"), Is.True, "a boss level with no mission list");
            }, LevelJson(boss: true), withMissionList: false);
        }

        private static MissionGroup NewGroup(Owned owned, bool requirementsMet, params bool[] rankedMissions)
        {
            var group = new MissionGroup();
            var requirements = new List<RequirementMissionGroupBase>();
            if (!requirementsMet) requirements.Add(owned.Track(ScriptableObject.CreateInstance<RequirementMissionGroupAlwaysHidden>()));
            Set(group, "m_requirements", requirements); // Reflection: authored group data
            var definitions = new List<MissionDefinition>();
            foreach (bool ranked in rankedMissions)
            {
                var mission = owned.Track(ScriptableObject.CreateInstance<MissionDefinition>());
                if (ranked) Set(mission, "m_ranks", owned.Track(ScriptableObject.CreateInstance<MissionRankDefinition>())); // Reflection: authored ranks
                definitions.Add(mission);
            }
            Set(group, "m_definitions", definitions); // Reflection: authored group data
            return group;
        }

        // BossRankLevelsHidden 0x6A1030: a non-boss level's ranks are shown; a boss level's ranks are hidden unless a mission group whose
        // requirements are all met holds a mission with ranks (found at 0x6A13F7, which makes the answer false at 0x6A1563).
        [TestCase(false, false, false, false, TestName = "ANonBossLevelsRanksAreShown")]
        [TestCase(true, false, false, true, TestName = "ABossLevelWithoutGroupsHidesItsRanks")]
        [TestCase(true, true, false, true, TestName = "ABossLevelWithoutRankedMissionsHidesItsRanks")]
        [TestCase(true, true, true, false, TestName = "ABossLevelWithARankedMissionShowsItsRanks")]
        public void BossRankLevelsAreHiddenUnlessAnUnlockedGroupHasARankedMission(bool boss, bool hasGroup, bool ranked, bool expectedHidden)
        {
            Run((owned, scene) =>
            {
                if (hasGroup)
                    Set(scene.Level.MissionList, "m_groups", new List<MissionGroup> { NewGroup(owned, true, false, ranked) });
                Set(scene.Widget, "m_parameters", scene.Parameters()); // Reflection: Setup would also build the bindings
                Assert.That(Call(scene.Widget, "BossRankLevelsHidden"), Is.EqualTo(expectedHidden));
            }, LevelJson(boss: boss));
        }

        // The search goes through every group: a group whose requirements fail is skipped even if it holds a ranked mission, and a
        // later qualifying group is still found.
        [Test]
        public void BossRankLevelsSkipGroupsWhoseRequirementsFailAndKeepSearching()
        {
            Run((owned, scene) =>
            {
                Set(scene.Widget, "m_parameters", scene.Parameters()); // Reflection: Setup would also build the bindings
                Set(scene.Level.MissionList, "m_groups", new List<MissionGroup> { NewGroup(owned, false, true) });
                Assert.That(Call(scene.Widget, "BossRankLevelsHidden"), Is.True, "the only ranked mission is in a locked group");

                Set(scene.Level.MissionList, "m_groups", new List<MissionGroup>
                {
                    NewGroup(owned, false, true), NewGroup(owned, true, false), NewGroup(owned, true, false, true),
                });
                Assert.That(Call(scene.Widget, "BossRankLevelsHidden"), Is.False, "the third group qualifies");
            }, LevelJson(boss: true));
        }

        // Select 0x6A1750 turns the toggle on (notifying listeners once) and, with no navigation manager registered, goes no further;
        // AddValueChangeListener 0x6A18D0 adds to the toggle's value event.
        [Test]
        public void SelectTurnsTheToggleOnAndNotifiesTheListenerOnce()
        {
            Run((owned, scene) =>
            {
                var received = new List<bool>();
                scene.Widget.AddValueChangeListener(received.Add);

                scene.Widget.Select();
                scene.Widget.Select(false);

                Assert.That(scene.Toggle.IsOn, Is.True);
                Assert.That(received, Is.EqualTo(new[] { true }), "the second select changes nothing");
            });
        }

        // DeselectWithoutNotify 0x6A18B0 clears the toggle through SetIsOnWithoutNotify, so the listener hears nothing.
        [Test]
        public void DeselectWithoutNotifyClearsTheToggleSilently()
        {
            Run((owned, scene) =>
            {
                var received = new List<bool>();
                scene.Widget.AddValueChangeListener(received.Add);
                scene.Widget.Select(false);
                received.Clear();

                scene.Widget.DeselectWithoutNotify();

                Assert.That(scene.Toggle.IsOn, Is.False);
                Assert.That(received, Is.Empty);
            });
        }

        // Action_OnNavigateTo 0x6A1870 selects the toggle only when it is interactable.
        [TestCase(true, true, TestName = "NavigatingSelectsAnInteractableWidget")]
        [TestCase(false, false, TestName = "NavigatingIgnoresANonInteractableWidget")]
        public void NavigatingToTheWidgetSelectsItOnlyWhileInteractable(bool interactable, bool expectedOn)
        {
            Run((owned, scene) =>
            {
                scene.Toggle.Interactable = interactable;
                scene.Widget.Action_OnNavigateTo();
                Assert.That(scene.Toggle.IsOn, Is.EqualTo(expectedOn));
            });
        }

        // ToggleWidgetNavigation 0x6A1660: turning it on enables the navigation unless the level is locked; turning it off disables it.
        [TestCase(true, false, true, TestName = "TurningNavigationOnEnablesAnUnlockedLevel")]
        [TestCase(true, true, false, TestName = "TurningNavigationOnLeavesALockedLevelDisabled")]
        [TestCase(false, false, false, TestName = "TurningNavigationOffDisablesAnUnlockedLevel")]
        [TestCase(false, true, false, TestName = "TurningNavigationOffDisablesALockedLevel")]
        public void TogglingTheNavigationEnablesItUnlessTheLevelIsLocked(bool on, bool locked, bool expectedEnabled)
        {
            Run((owned, scene) =>
            {
                SetProgress(scene, 0, 1);
                scene.Widget.Setup(scene.Parameters(locked: locked));
                scene.Navigation.enabled = !expectedEnabled;

                scene.Widget.ToggleWidgetNavigation(on);

                Assert.That(scene.Navigation.enabled, Is.EqualTo(expectedEnabled));
            }, withMissionList: false);
        }

        // OnUnlockComplete 0x6A16A0 turns an off toggle on (so listeners are told once) and leaves one that is already on alone.
        [Test]
        public void CompletingTheUnlockTurnsTheToggleOnOnce()
        {
            Run((owned, scene) =>
            {
                var received = new List<bool>();
                scene.Widget.AddValueChangeListener(received.Add);

                Call(scene.Widget, "OnUnlockComplete");
                Call(scene.Widget, "OnUnlockComplete");

                Assert.That(scene.Toggle.IsOn, Is.True);
                Assert.That(received, Is.EqualTo(new[] { true }));
            });
        }
    }
}
