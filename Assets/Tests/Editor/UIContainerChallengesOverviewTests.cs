using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerChallengesOverview (Game.Runtime 0x02000887, x86-64 0x61DA20..0x61F6E0, closures 0x02000888 and 0x02000889) as a real
    // component on an inactive owned host over the genuine ChallengeManager (registered next to the level-requirement world's App,
    // SaveManager, DataManager, LevelManager and MissionManager), real toggle widgets cloned from owned prefab hosts into an active
    // toggle group, and the real challenge, bonus and level containers kept under an inactive parent so that they never enable. The
    // shadow FTUE challenge is complete when its mission's save record says so. Challenge states are built by their constructor and
    // given their best score and time through UpdateScoreInfo. The serialized fields are authored data. Every update schedules the
    // navigation refresh on the process coroutine host, which is inactive here and refuses the coroutine with a logged error.
    // Not covered: the surplus-toggle destruction of OnChallengesUpdated (Destroy is refused in Edit Mode), the leaderboard panel and
    // the info popup and bonus XP events of OnChallengesUpdated, the test mode panel, the navigation manager's selection, and the
    // launch data of the shadow FTUE challenge (it needs the FTUE cutscene's level).
    public sealed class UIContainerChallengesOverviewTests
    {
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");

        private const Strings RefreshLabel = Strings.MENU_TAILS_CHALLENGES_LABEL_NEW_CHALLENGES;
        private const Strings BonusDescription = Strings.MENU_TAILS_CHALLENGES_INFO_BODY1;
        private const Strings ChallengeDescription = Strings.MENU_TAILS_CHALLENGES_INFO_HEADER;

        private static readonly Dictionary<Strings, string> Texts = new Dictionary<Strings, string>
        {
            { RefreshLabel, "Refreshes in" },
            { BonusDescription, "Finish {0} challenges" },
            { Strings.MENU_TAILS_CHALLENGES_XP, "{0} XP" },
            { Strings.MISSION_TIMER_FORMAT_DESCRIPTION, "T{0}-{1}-{2}" },
            { ChallengeDescription, "Run: {0}/{1}" },
            { Strings.NONE, "none" },
        };

        private sealed class Rig
        {
            public LevelRequirementWorld World;
            public Owned Owned;
            public ChallengeManager Manager;
            public UIContainerChallengesOverview Container;
            public UIToggleGroup Group;
            public UIContainerChallenge Info;
            public UIContainerChallengeBonus Bonus;
            public UIContainerLevelDefinition LevelView;
            public UIModernEvent BonusXpEvent, ShowInfoEvent;
            public ChallengeDefinition ShadowChallenge;
            public GameplayLevelDefinition Level;
            public MissionDefinition Mission;

            public List<UIWidgetSDTToggleChallenge> Toggles => Get<List<UIWidgetSDTToggleChallenge>>(Container, "m_uiWidgetToggleChallenges");
            public UIWidgetSDTToggleChallenge BonusToggle => Get<UIWidgetSDTToggleChallenge>(Container, "m_bonusToggle");
            public ChallengeState Selected => Get<ChallengeState>(Container, "m_selectedChallenge");
            public List<ChallengeState> States => Get<List<ChallengeState>>(Manager, "m_currentChallenges");
        }

        // The services come first (the world), then the manager is registered before the container caches its reference; the container
        // is built last. With shadowFtueDone false the FTUE challenge's mission is still to be played, so the shadow FTUE is shown.
        private static void Run(Action<Rig> body, bool shadowFtueDone = true) => OwnedStringTable.Run(Texts, () => LevelRequirementWorld.Run((owned, world) =>
        {
            var rig = new Rig { World = world, Owned = owned };
            owned.LeaseRow<VisualQualityManager_SDT>();
            owned.LeaseRow<CutsceneManager>();
            owned.LeaseRow<OrnamentManager>();
            owned.LeaseRow<UINavigationManager>();
            rig.Manager = owned.Register(owned.Lease(new OwnedTimeScaledObjects("lucid-challenges-overview")).Add<ChallengeManager>("manager"));
            rig.Mission = world.NewMission("lucid-overview-mission");
            rig.Level = world.NewLevel("lucid-overview-level", false, world.NewGroup("lucid-overview-group", rig.Mission));
            rig.ShadowChallenge = NewChallenge(rig, "lucid-overview-shadow-challenge");
            Set(rig.Manager, "m_shadowFtueChallenge", rig.ShadowChallenge); // Reflection: serialized reference
            Set(rig.Manager, "m_challengeCycleDefinition", Create<ChallengeCycleDefinition>(owned, "{\"m_periodBonusXP\":250}"));
            world.SetComplete(rig.Level, rig.Mission, shadowFtueDone);

            // The panels sit under an inactive parent: they can be switched on and off but never enable.
            GameObject panels = owned.Host("Lucid overview panels", false);
            rig.Info = owned.Host("Lucid overview info", false, panels.transform).AddComponent<UIContainerChallenge>();
            rig.Bonus = owned.Host("Lucid overview bonus", false, panels.transform).AddComponent<UIContainerChallengeBonus>();
            rig.LevelView = owned.Host("Lucid overview level view", false, panels.transform).AddComponent<UIContainerLevelDefinition>();
            Set(rig.Bonus, "m_descriptionText", BonusDescription); // Reflection: serialized string
            rig.Group = owned.Host("Lucid overview toggle group", true).AddComponent<UIToggleGroup>();
            GameObject prefabs = owned.Host("Lucid overview prefabs", false);
            rig.BonusXpEvent = NewEvent(owned, "lucid-overview-bonus-xp");
            rig.ShowInfoEvent = NewEvent(owned, "lucid-overview-show-info");

            rig.Container = owned.Host("Lucid challenges overview", false).AddComponent<UIContainerChallengesOverview>();
            // Reflection: serialized references and strings of the authored container
            Set(rig.Container, "m_containerNavigation", owned.Host("Lucid overview navigation", false).AddComponent<UIContainerNavigation>());
            Set(rig.Container, "m_challengeTogglesRoot", rig.Group);
            Set(rig.Container, "m_challengeTogglePrefab", NewTogglePrefab(owned, prefabs, "Lucid overview toggle prefab"));
            Set(rig.Container, "m_challengeToggleBonusPrefab", NewTogglePrefab(owned, prefabs, "Lucid overview bonus prefab"));
            Set(rig.Container, "m_challengeInfoContainer", rig.Info);
            Set(rig.Container, "m_challengeBonusInfoContainer", rig.Bonus);
            Set(rig.Container, "m_levelDefinitionView", rig.LevelView);
            Set(rig.Container, "m_refreshesInLabel", RefreshLabel);
            Set(rig.Container, "m_bonusXpAvailableEvent", rig.BonusXpEvent);
            Set(rig.Container, "m_showInfoEvent", rig.ShowInfoEvent);
            Set(rig.Container, "m_testModeDisplay", owned.Host("Lucid overview test mode", false));
            body(rig);
        }));

        private static UIModernEvent NewEvent(Owned owned, string guid)
        {
            var uiEvent = owned.Track(ScriptableObject.CreateInstance<UIModernEvent>());
            Set(uiEvent, "m_guid", guid);
            return uiEvent;
        }

        // A toggle prefab: an active host under an inactive parent, so that clones enable but the prefab never does.
        private static UIWidgetSDTToggleChallenge NewTogglePrefab(Owned owned, GameObject parent, string name)
        {
            GameObject host = owned.Host(name, true, parent.transform);
            var widget = host.AddComponent<UIWidgetSDTToggleChallenge>();
            var toggle = host.GetComponent<UIToggle>();
            var element = host.AddComponent<UIToggleGroupElement>();
            Set(toggle, "m_groupElement", element); // Reflection: serialized references the editor fills in
            Set(element, "m_toggle", toggle);
            Set(widget, "m_toggle", toggle);
            return widget;
        }

        private static ChallengeDefinition NewChallenge(Rig rig, string guid)
        {
            var definition = Create<ChallengeDefinition>(rig.Owned,
                "{\"m_guid\":\"" + guid + "\",\"m_xpReward\":120,\"m_timeLimitSeconds\":90,\"m_description\":" + (int)ChallengeDescription + "}");
            Set(definition, "m_mission", rig.Mission); // Reflection: serialized references and lists
            Set(definition, "m_level", rig.Level);
            Set(definition, "m_characterSelection", new CharacterId[0]);
            Set(definition, "m_startPositions", new LevelStartPositionDefinition[0]);
            return definition;
        }

        // A challenge with the given best score and time; the manager's live list receives it when asked.
        private static ChallengeState NewState(Rig rig, string guid, int score, float time, bool addToManager = true)
        {
            var state = new ChallengeState(0, NewChallenge(rig, guid), 0, null, 0);
            state.OnAwardXP = (challenge, first, ringXp) => { };
            state.OnUpdateScore = challenge => { };
            state.UpdateScoreInfo(score, time, 0, true);
            state.OnUpdateScore = null;
            // Reflection: the manager's list is filled by its cycle refresh, which needs the leaderboard integration.
            if (addToManager) rig.States.Add(state);
            return state;
        }

        // Start runs the whole first update; the update schedules the navigation refresh on the refused coroutine host.
        private static void Start(Rig rig)
        {
            LogAssert.Expect(LogType.Error, RefusedCoroutine);
            Call(rig.Container, "Start");
        }

        private static void Update(Rig rig)
        {
            LogAssert.Expect(LogType.Error, RefusedCoroutine);
            rig.Manager.OnChallengesUpdated(rig.Manager.GetChallengeStates());
        }

        // dump.cs TypeDefIndex 2184, 2182 and 2183 and the DummyDll rows (fields 0x04002165..0x0400217f, methods 0x0600313d..0x0600314e).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            var serialized = new[]
            {
                "m_containerNavigation", "m_challengeTogglesRoot", "m_challengeTogglePrefab", "m_challengeToggleBonusPrefab", "m_challengeInfoContainer",
                "m_challengeBonusInfoContainer", "m_levelDefinitionView", "m_challengeLeaderboardContainer", "m_refreshesInLabel",
                "m_bonusXpAvailableEvent", "m_showInfoEvent", "m_testModeDisplay",
            };
            AssertMembers(typeof(UIContainerChallengesOverview), 0x00100101,
                serialized.Select(name => (name, 0x0001))
                    .Concat(new[] { "RefreshesInLabel", "ValidChallengeSelected", "CycleDebugLabel", "ShowShadowFTUE" }.Select(name => (name, 0x0026)))
                    .Concat(new (string, int)[]
                    {
                        ("m_challengeManagerRef", 0x0001), ("m_uiWidgetToggleChallenges", 0x0021), ("m_uiWidgetToggleChallengeBonus", 0x0001),
                        ("m_bonusToggle", 0x0001), ("m_selectedChallenge", 0x0001), ("m_hasShownBonusXpAwarded", 0x0001),
                        ("m_navigationManagerRef", 0x0021),
                    }).ToArray(),
                new (string, int)[]
                {
                    ("Start", 0x0081), ("OnDestroy", 0x0081), ("OnChallengeManagerValid", 0x0081), ("OnChallengesUpdated", 0x0081),
                    ("ToggleChallenge", 0x0081), ("ToggleBonus", 0x0081), ("UpdateLevelPreview", 0x0081), ("TryNavigateToWidget", 0x0081),
                    ("Action_SetChallengeLaunchData", 0x0086), ("ValidateChallengeState", 0x0081), ("Action_ClearChallengeLaunchData", 0x0086),
                    ("ShowInfoPopup", 0x0086), (".ctor", 0x1886), ("<OnChallengesUpdated>b__26_0", 0x0081),
                });
            AssertIl2CppOptions(typeof(UIContainerChallengesOverview), 2, 1);
            Assert.That(typeof(UIContainerChallengesOverview).BaseType, Is.EqualTo(typeof(UIContainer)));
            foreach ((string name, string tooltip) in new[]
            {
                ("m_challengeTogglesRoot", "Parent to spawn the challenge toggles into"),
                ("m_challengeTogglePrefab", "Toggle challenge prefab to spawn for the active challenges"),
                ("m_challengeToggleBonusPrefab", "Toggle challenge prefab to spawn that displays the bonus info for the challenge period when selected"),
                ("m_challengeInfoContainer", "Container for displaying info about the selected challenge"),
                ("m_challengeBonusInfoContainer", "Container for displaying challenge period bonus info when that toggle is selected"),
                ("m_levelDefinitionView", "Level definition view for displaying image previewing level for selected challenge"),
                ("m_challengeLeaderboardContainer", "Displays the current score for the challenge period and allows opening of the leaderboard"),
                ("m_bonusXpAvailableEvent", "UIEvent to send when there is bonus XP to notify the user about"),
                ("m_showInfoEvent", "UIEvent to send to show the challenges info popup"),
                ("m_testModeDisplay", "Object to switch on if test mode is active, and switch off when inactive."),
            })
                Assert.That(Tooltip(typeof(UIContainerChallengesOverview).GetField(name, Declared)), Is.EqualTo(tooltip), name);
        }

        // The two closure classes of OnChallengesUpdated (one per scope: the shadow FTUE block and the loop body).
        [Test]
        public void TheClosureClassesMatchTheOriginalMetadata()
        {
            foreach (string name in new[] { "<>c__DisplayClass26_0", "<>c__DisplayClass26_1" })
            {
                Type closure = typeof(UIContainerChallengesOverview).GetNestedType(name, BindingFlags.NonPublic);
                Assert.That(closure, Is.Not.Null, name);
                AssertMembers(closure, 0x00100103, new (string, int)[] { ("challengeState", 0x0006), ("<>4__this", 0x0006) },
                    new (string, int)[] { (".ctor", 0x1886), ("<OnChallengesUpdated>b__" + (name.EndsWith("_0") ? 1 : 2), 0x0083) });
            }
        }

        // The constructor 0x61F4F0 creates the four bindables (+0x98, +0xA0, +0xA8, +0xB0), an empty toggle list (+0xC0) and takes the
        // navigation manager reference (+0xE8).
        [Test]
        public void ConstructionCreatesTheBindablesTheListAndTheNavigationReference()
        {
            Run(rig =>
            {
                var bindables = new object[] { rig.Container.RefreshesInLabel, rig.Container.ValidChallengeSelected, rig.Container.CycleDebugLabel, rig.Container.ShowShadowFTUE };
                Assert.That(bindables, Has.All.Not.Null);
                Assert.That(bindables.Distinct().Count(), Is.EqualTo(4));
                Assert.That(rig.Toggles, Is.Empty);
                Assert.That(Get<SystemRef<UINavigationManager>>(rig.Container, "m_navigationManagerRef"), Is.SameAs(ProcessManager.GetSystemRef<UINavigationManager>()));
            }, true);
        }

        // Start 0x61DA20 hides the three panels (SetActive false at 0x61DB00, 0x61DB17, 0x61DB2E), localises the refresh label, clears
        // the valid flag and removes the shadow FTUE selection flag from the application storage (RemoveValue at 0x61DCA2).
        [Test]
        public void StartHidesThePanelsLocalisesTheLabelAndClearsTheShadowFtueFlag()
        {
            Run(rig =>
            {
                rig.Info.gameObject.SetActive(true);
                rig.Bonus.gameObject.SetActive(true);
                rig.LevelView.gameObject.SetActive(true);
                IGraphStorage storage = ProcessManager.GetSystem<App>().Storage;
                storage.SetValue(AppFSMKeys.SelectShadowFTUEChallenge, true);

                Start(rig);

                Assert.That(rig.Container.RefreshesInLabel.Value, Is.EqualTo("Refreshes in"));
                Assert.That(rig.Container.ValidChallengeSelected.Value, Is.False);
                Assert.That(storage.GetValue(AppFSMKeys.SelectShadowFTUEChallenge, false, false), Is.False, "the flag was removed, leaving the default");
                Assert.That(rig.Info.gameObject.activeSelf, Is.False);
                Assert.That(rig.Bonus.gameObject.activeSelf, Is.False);
                Assert.That(rig.LevelView.gameObject.activeSelf, Is.False);
            });
        }

        // Start subscribes the container to the manager's updates (OnChallengeManagerValid 0x61DE7B) and OnDestroy removes it again (0x61DD78).
        [Test]
        public void TheContainerFollowsTheManagerUntilItIsDestroyed()
        {
            Run(rig =>
            {
                Start(rig);
                Assert.That(rig.Manager.OnChallengesUpdated, Is.Not.Null);
                NewState(rig, "lucid-overview-follow", 0, 0f);

                Update(rig);
                Assert.That(rig.Toggles.Count, Is.EqualTo(1), "an update through the manager reaches the container");

                Call(rig.Container, "OnDestroy");
                Assert.That(rig.Manager.OnChallengesUpdated, Is.Null);
            });
        }

        // OnChallengesUpdated 0x61DEE0 (normal mode): one toggle per challenge, numbered from 1; a challenge is new while it was never
        // played (get_Played at 0x61E5C8, inverted at 0x61E5E2) and complete once its best time is above zero (ucomiss against 0.0 at
        // 0x61E5B7, constant 0x29A6BEC); the first challenge that is not complete is selected, which is told apart from the first
        // unplayed one by a challenge that was played for a score only.
        [Test]
        public void EachChallengeGetsANumberedToggleAndTheFirstUnfinishedOneIsSelected()
        {
            Run(rig =>
            {
                NewState(rig, "lucid-overview-done", 100, 12.5f);
                NewState(rig, "lucid-overview-scored", 10, 0f);
                NewState(rig, "lucid-overview-fresh", 0, 0f);

                Start(rig);

                List<UIWidgetSDTToggleChallenge> toggles = rig.Toggles;
                Assert.That(toggles.Count, Is.EqualTo(3));
                Assert.That(toggles.Select(toggle => toggle.Label.Value).ToArray(), Is.EqualTo(new[] { "1", "2", "3" }));
                Assert.That(toggles.Select(toggle => toggle.IsNew.Value).ToArray(), Is.EqualTo(new[] { false, false, true }));
                Assert.That(toggles.Select(toggle => toggle.IsComplete.Value).ToArray(), Is.EqualTo(new[] { true, false, false }));
                Assert.That(toggles.Select(toggle => toggle.IsBonus.Value || toggle.IsShadow.Value).ToArray(), Is.EqualTo(new[] { false, false, false }));
                Assert.That(toggles.Select(toggle => toggle.IsSelected()).ToArray(), Is.EqualTo(new[] { false, true, false }));
                Assert.That(rig.Container.ShowShadowFTUE.Value, Is.False);
                Assert.That(rig.Container.ValidChallengeSelected.Value, Is.True);
                Assert.That(rig.BonusToggle.IsSelected(), Is.False);
                Assert.That(toggles.All(toggle => toggle.transform.parent == rig.Group.transform), "the toggles are spawned into the group");
                Assert.That(toggles.All(toggle => toggle.GroupElement.Group == rig.Group), "and joined to it");
            });
        }

        // The bonus toggle follows the challenge toggles (SetAsLastSibling at 0x61E517) and carries the bonus marker with no label, and
        // is complete only when every challenge is.
        [Test]
        public void TheBonusToggleIsLastAndCompleteOnlyWhenEveryChallengeIs()
        {
            Run(rig =>
            {
                NewState(rig, "lucid-overview-done", 100, 12.5f);
                NewState(rig, "lucid-overview-open", 0, 0f);

                Start(rig);

                UIWidgetSDTToggleChallenge bonus = rig.BonusToggle;
                Assert.That(bonus.transform.GetSiblingIndex(), Is.EqualTo(bonus.transform.parent.childCount - 1));
                Assert.That(bonus.IsBonus.Value, Is.True);
                Assert.That(bonus.IsNew.Value, Is.False);
                Assert.That(bonus.IsShadow.Value, Is.False);
                Assert.That(bonus.Label.Value, Is.Null);
                Assert.That(bonus.IsComplete.Value, Is.False, "one challenge is still open");
                Assert.That(rig.Bonus.BonusXP.Value, Is.EqualTo(250), "GetBonusXP of the cycle definition");
                Assert.That(rig.Bonus.ChallengeCount.Value, Is.EqualTo(2));
                Assert.That(rig.Bonus.Earned.Value, Is.False);
            });
        }

        // With every challenge complete no challenge toggle is selected: the bonus toggle is (0x61EA90..0x61EBF7), without raising the
        // valid flag, and the bonus panel is told that the bonus is earned.
        [Test]
        public void WhenEveryChallengeIsCompleteTheBonusToggleIsSelected()
        {
            Run(rig =>
            {
                NewState(rig, "lucid-overview-done-one", 100, 12.5f);
                NewState(rig, "lucid-overview-done-two", 50, 30f);

                Start(rig);

                Assert.That(rig.Toggles.Select(toggle => toggle.IsSelected()).ToArray(), Is.EqualTo(new[] { false, false }));
                Assert.That(rig.BonusToggle.IsSelected(), Is.True);
                Assert.That(rig.BonusToggle.IsComplete.Value, Is.True);
                Assert.That(rig.Bonus.Earned.Value, Is.True);
                Assert.That(rig.Container.ValidChallengeSelected.Value, Is.False);
            });
        }

        // Without any challenge the bonus is complete (the flag starts true, 0x61E526) and is selected.
        [Test]
        public void WithoutChallengesTheBonusIsCompleteAndSelected()
        {
            Run(rig =>
            {
                Start(rig);

                Assert.That(rig.Toggles, Is.Empty);
                Assert.That(rig.BonusToggle.IsComplete.Value, Is.True);
                Assert.That(rig.BonusToggle.IsSelected(), Is.True);
                Assert.That(rig.Bonus.ChallengeCount.Value, Is.EqualTo(0));
                Assert.That(rig.Bonus.Earned.Value, Is.True);
            });
        }

        // The update ends by clearing the selection and hiding both info panels (0x61E924..0x61E96B) before it selects a toggle.
        [Test]
        public void AnUpdateClearsTheSelectedChallengeAndHidesThePanels()
        {
            Run(rig =>
            {
                ChallengeState state = NewState(rig, "lucid-overview-open", 0, 0f);
                Start(rig);
                Call(rig.Container, "ToggleChallenge", state, true);
                Assert.That(rig.Selected, Is.SameAs(state));
                Assert.That(rig.Info.gameObject.activeSelf, Is.True);

                Update(rig);

                Assert.That(rig.Selected, Is.Null);
                Assert.That(rig.Info.gameObject.activeSelf, Is.False);
                Assert.That(rig.Bonus.gameObject.activeSelf, Is.False);
                Assert.That(rig.LevelView.gameObject.activeSelf, Is.False);
            });
        }

        // More challenges than toggles create more toggles (the loop at 0x61E1E4) and the bonus toggle is kept.
        [Test]
        public void MoreChallengesGrowTheListAndKeepTheBonusToggle()
        {
            Run(rig =>
            {
                NewState(rig, "lucid-overview-first", 0, 0f);
                Start(rig);
                UIWidgetSDTToggleChallenge bonus = rig.BonusToggle;
                UIWidgetSDTToggleChallenge first = rig.Toggles[0];
                NewState(rig, "lucid-overview-second", 0, 0f);

                Update(rig);

                Assert.That(rig.Toggles.Count, Is.EqualTo(2));
                Assert.That(rig.Toggles[0], Is.SameAs(first));
                Assert.That(rig.BonusToggle, Is.SameAs(bonus));
                Assert.That(rig.Toggles.Select(toggle => toggle.Label.Value).ToArray(), Is.EqualTo(new[] { "1", "2" }));
            });
        }

        // OnChallengesUpdated (shadow FTUE): ShouldShowShadowFTUE gives a count of one whatever the manager lists; the single toggle is
        // the shadow one (new, not complete, no label: string.Empty at 0x61E3B2..0x61E3C3) and is selected; there is no bonus toggle.
        [Test]
        public void TheShadowFtueShowsOneShadowToggle()
        {
            Run(rig =>
            {
                NewState(rig, "lucid-overview-listed-one", 0, 0f);
                NewState(rig, "lucid-overview-listed-two", 0, 0f);

                Start(rig);

                Assert.That(rig.Container.ShowShadowFTUE.Value, Is.True);
                Assert.That(rig.Toggles.Count, Is.EqualTo(1));
                UIWidgetSDTToggleChallenge toggle = rig.Toggles[0];
                Assert.That(toggle.IsShadow.Value, Is.True);
                Assert.That(toggle.IsNew.Value, Is.True);
                Assert.That(toggle.IsComplete.Value, Is.False);
                Assert.That(toggle.IsBonus.Value, Is.False);
                Assert.That(toggle.Label.Value, Is.EqualTo(string.Empty));
                Assert.That(toggle.IsSelected(), Is.True);
                Assert.That(rig.Container.ValidChallengeSelected.Value, Is.True);
                Assert.That(rig.BonusToggle, Is.Null, "the shadow FTUE has no bonus toggle");
            }, false);
        }

        // ToggleBonus 0x61F0D0: selecting the bonus shows the bonus panel, hides the challenge panel and drops the valid flag;
        // deselecting only hides the bonus panel (the jump to SetActive at 0x61F13B).
        [Test]
        public void SelectingTheBonusSwapsThePanelsAndDeselectingHidesOnlyTheBonusPanel()
        {
            Run(rig =>
            {
                rig.Container.ValidChallengeSelected.Value = true;
                rig.Info.gameObject.SetActive(true);

                Call(rig.Container, "ToggleBonus", true);
                Assert.That(rig.Bonus.gameObject.activeSelf, Is.True);
                Assert.That(rig.Info.gameObject.activeSelf, Is.False);
                Assert.That(rig.Container.ValidChallengeSelected.Value, Is.False);

                rig.Container.ValidChallengeSelected.Value = true;
                rig.Info.gameObject.SetActive(true);
                Call(rig.Container, "ToggleBonus", false);
                Assert.That(rig.Bonus.gameObject.activeSelf, Is.False);
                Assert.That(rig.Info.gameObject.activeSelf, Is.True, "untouched");
                Assert.That(rig.Container.ValidChallengeSelected.Value, Is.True, "untouched");
            });
        }

        // ValidateChallengeState 0x61F040: while the shadow FTUE is to be shown only the FTUE challenge is valid (the definitions are
        // compared by their guid through ScriptableObjectWithGuid's equality, 0x61F0AD); otherwise the challenge must be in the manager's list.
        [Test]
        public void ANormalChallengeIsValidWhileTheManagerListsIt()
        {
            Run(rig =>
            {
                ChallengeState listed = NewState(rig, "lucid-overview-listed", 0, 0f);
                ChallengeState stranger = NewState(rig, "lucid-overview-stranger", 0, 0f, false);

                Assert.That(Call(rig.Container, "ValidateChallengeState", listed), Is.True);
                Assert.That(Call(rig.Container, "ValidateChallengeState", stranger), Is.False);
            }, true);
        }

        [Test]
        public void WhileTheShadowFtueIsToBeShownOnlyItsChallengeIsValid()
        {
            Run(rig =>
            {
                ChallengeState listed = NewState(rig, "lucid-overview-listed", 0, 0f);
                var shadow = new ChallengeState(0, rig.ShadowChallenge, 0, null, 0);

                Assert.That(Call(rig.Container, "ValidateChallengeState", shadow), Is.True);
                Assert.That(Call(rig.Container, "ValidateChallengeState", listed), Is.False, "listed, but not the shadow FTUE challenge");
            }, false);
        }

        // ToggleChallenge 0x61EDE0: selecting a valid challenge shows its panel and level preview and hides the bonus panel; deselecting
        // another challenge changes nothing; deselecting the selected one clears the selection, hides the panels and drops the flag.
        [Test]
        public void SelectingAndDeselectingAChallengeShowsAndHidesItsPanels()
        {
            Run(rig =>
            {
                ChallengeState first = NewState(rig, "lucid-overview-first", 0, 0f);
                ChallengeState second = NewState(rig, "lucid-overview-second", 0, 0f);
                rig.Bonus.gameObject.SetActive(true);

                Call(rig.Container, "ToggleChallenge", first, true);

                Assert.That(rig.Selected, Is.SameAs(first));
                Assert.That(rig.Info.gameObject.activeSelf, Is.True);
                Assert.That(rig.Bonus.gameObject.activeSelf, Is.False);
                Assert.That(rig.Container.ValidChallengeSelected.Value, Is.True);
                Assert.That(rig.LevelView.gameObject.activeSelf, Is.True, "UpdateLevelPreview shows the level view");
                Assert.That(Get<GameplayLevelDefinition>(rig.LevelView, "m_levelDefinition"), Is.SameAs(rig.Level));

                Call(rig.Container, "ToggleChallenge", second, false);

                Assert.That(rig.Selected, Is.SameAs(first), "another challenge was deselected");
                Assert.That(rig.Container.ValidChallengeSelected.Value, Is.True);

                Call(rig.Container, "ToggleChallenge", first, false);

                Assert.That(rig.Selected, Is.Null);
                Assert.That(rig.Info.gameObject.activeSelf, Is.False);
                Assert.That(rig.Container.ValidChallengeSelected.Value, Is.False);
                Assert.That(rig.LevelView.gameObject.activeSelf, Is.False);
            });
        }

        // Selecting a challenge that is not valid clears the selection and the flag and returns at once (the tail call at 0x61EEC3): the
        // panels and the level preview are left as they were.
        [Test]
        public void SelectingAnInvalidChallengeClearsTheSelectionAndLeavesThePanels()
        {
            Run(rig =>
            {
                ChallengeState listed = NewState(rig, "lucid-overview-listed", 0, 0f);
                ChallengeState stranger = NewState(rig, "lucid-overview-stranger", 0, 0f, false);
                Call(rig.Container, "ToggleChallenge", listed, true);
                rig.LevelView.gameObject.SetActive(true);

                Call(rig.Container, "ToggleChallenge", stranger, true);

                Assert.That(rig.Selected, Is.Null);
                Assert.That(rig.Container.ValidChallengeSelected.Value, Is.False);
                Assert.That(rig.Info.gameObject.activeSelf, Is.True, "the info panel was not touched");
                Assert.That(rig.LevelView.gameObject.activeSelf, Is.True, "nor the level preview");
            });
        }

        // TryNavigateToWidget 0x61ED60 returns when the navigation manager reference is empty (IsNull at 0x61EDA6).
        [Test]
        public void WithoutANavigationManagerNothingIsSelected()
        {
            Run(rig => Assert.That(() => Call(rig.Container, "TryNavigateToWidget", rig.Owned.Host("Lucid overview toggle", false).AddComponent<UIWidgetSDTToggleChallenge>()),
                Throws.Nothing));
        }

        // Action_SetChallengeLaunchData 0x61F140 without a selection only drops the valid flag (0x61F256).
        [Test]
        public void LaunchDataWithoutASelectionOnlyClearsTheValidFlag()
        {
            Run(rig =>
            {
                rig.Container.ValidChallengeSelected.Value = true;

                rig.Container.Action_SetChallengeLaunchData();

                Assert.That(rig.Container.ValidChallengeSelected.Value, Is.False);
                Assert.That(rig.World.Missions.ActiveMissionContext, Is.Null);
            });
        }

        // A selected challenge that is no longer valid is dropped the same way (0x61F241) and the application is left alone.
        [Test]
        public void LaunchDataForAnInvalidSelectionDropsTheSelection()
        {
            Run(rig =>
            {
                ChallengeState stranger = NewState(rig, "lucid-overview-stranger", 0, 0f, false);
                Set(rig.Container, "m_selectedChallenge", stranger); // Reflection: ToggleChallenge refuses to select an invalid challenge
                rig.Container.ValidChallengeSelected.Value = true;

                rig.Container.Action_SetChallengeLaunchData();

                Assert.That(rig.Selected, Is.Null);
                Assert.That(rig.Container.ValidChallengeSelected.Value, Is.False);
                Assert.That(rig.World.Missions.ActiveMissionContext, Is.Null);
            });
        }

        // A valid selection stores the shadow FTUE flag (false here, 0x61F3AD), the level to load (SetupFsmVarsToLoadLevel at 0x61F401, with the
        // challenge's level and no loading image override) and a challenge mission context built with the manager's test mode flag
        // (byte +0xEC read at 0x61F437) for the mission manager; Action_ClearChallengeLaunchData (0x61F4E2) then clears the context.
        [Test]
        public void LaunchDataHandsTheChallengeToTheApplicationAndClearingRemovesIt()
        {
            Run(rig =>
            {
                ChallengeState state = NewState(rig, "lucid-overview-launch", 0, 0f);
                Call(rig.Container, "ToggleChallenge", state, true);

                rig.Container.Action_SetChallengeLaunchData();

                IGraphStorage storage = ProcessManager.GetSystem<App>().Storage;
                Assert.That(storage.GetValue<LevelManagerLoadLevelParameters>(AppFSMKeys.LevelLoadParameters).LevelDefinition, Is.SameAs(rig.Level));
                Assert.That(storage.GetValue<UIContainerLoadingParameters>(AppFSMKeys.UIContainerParameters).LevelDefinition, Is.SameAs(rig.Level));
                Assert.That(storage.GetValue(AppFSMKeys.SelectShadowFTUEChallenge, true, false), Is.False);
                Assert.That(rig.World.Missions.ActiveMissionContext, Is.InstanceOf<MissionContextChallenge>());
                Assert.That(rig.World.Missions.ActiveMissionContext.LevelDefinition, Is.SameAs(rig.Level));

                rig.Container.Action_ClearChallengeLaunchData();

                Assert.That(rig.World.Missions.ActiveMissionContext, Is.Null);
            });
        }

        // ShowInfoPopup 0x61EC30 adds the info event to the application's pending UI events without duplicating it (AddUniqueUIModernEvent).
        [Test]
        public void ShowingTheInfoPopupQueuesTheInfoEventOnce()
        {
            Run(rig =>
            {
                var pending = new List<UIModernEvent>();
                ProcessManager.GetSystem<App>().Storage.SetValue(AppFSMKeys.UIModernEvents, pending);

                rig.Container.ShowInfoPopup();
                rig.Container.ShowInfoPopup();

                Assert.That(pending, Is.EqualTo(new[] { rig.ShowInfoEvent }));
            });
        }
    }
}
