using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.MissionTrackerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // The debug menu container (Game.Runtime TypeDef 0x020008a9, x86-64 0x62B6F0..0x62E960) as a real component on an owned inactive host.
    // The respawn controls run over a real CharacterManager and Character (CharacterTriggerTestSupport.WithManager) and a registered
    // LevelRespawnPoints holding real RespawnPoints. The mission buttons run over the real MissionManager of OriginalPersistentLevelObjectTests
    // .WithMissions with a registered App, and a regular mission context for a definition set up in the manager. Closing the old
    // buttons calls Object.Destroy, which Edit Mode refuses with an error, so those cases expect it once per existing child.
    // SetupForMission runs over a MissionList on a level-select level of a GameLevels set on the LevelManager, with ranks S 10 s and A 20 s.
    // Not covered: Start (it queues callbacks on the ornament, challenge and dream power store rows), OnChallengeManagerValid (the
    // toggle initialisation from FeatureState, 0x62c8de), AddXP, ResetXP, the cycle buttons, OnOrnamentManagerValid,
    // OnDreamPowerStoreManagerValid and the Action_ methods that forward to those systems, including the bool to feature state mapping
    // of Action_OnChallengeFeatureToggle (0x62df1e: true is UnlockedShowInterstitial, false Locked). They need a started ChallengeManager
    // and DreamPowerStoreManager; setting a feature state reaches the shadow tutorial challenge, the leaderboard integration and the
    // save, which an inactive owned manager does not have, and the state has no setter to reach it by,
    // the found branch of CanSetupForMissionSelect (it needs a UIContainerMissionSelect),
    // the widget-navigation branches of the mission buttons, and Action_OnWidgetSelected (it needs a HLScrollRectSelection).
    public sealed class UIContainerDebugTests
    {
        private static readonly Regex RefusedDestroy = new Regex("^Destroy may not be called from edit mode");
        private static readonly MethodInfo NextPoint = typeof(UIContainerDebug).GetMethod("GetNextRespawnPoint", BindingFlags.Static | BindingFlags.NonPublic);
        private const string MissionGuid = "lucid-debug-container-mission";
        private const string MissionName = "Lucid debug mission";

        private static void SetSerialized(Object target, string field, Action<SerializedProperty> assign)
        {
            var serialized = new SerializedObject(target);
            assign(serialized.FindProperty(field));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RespawnPoint Next(Transform previous)
        {
            try
            {
                return (RespawnPoint)NextPoint.Invoke(null, new object[] { previous });
            }
            catch (TargetInvocationException wrapped) when (wrapped.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(wrapped.InnerException).Throw();
                throw;
            }
        }

        // A respawn point on its own object with the collider it requires, or on a shared object (a second point on the same transform).
        private static RespawnPoint NewPoint(Owned owned, string name, GameObject shared = null)
        {
            GameObject host = shared ?? owned.Host(name, false);
            if (shared == null) host.AddComponent<BoxCollider>();
            return host.AddComponent<RespawnPoint>();
        }

        private static RespawnPoint[] NewPoints(Owned owned, LevelRespawnPoints list, int count)
        {
            RespawnPoint[] points = Enumerable.Range(0, count).Select(index => NewPoint(owned, "Lucid respawn " + index)).ToArray();
            foreach (RespawnPoint point in points) list.Add(point);
            return points;
        }

        // The constructor 0x62DFE0 stores -1 at +0x120 after both bindables and the five typed references; the container caches
        // references to the MissionManager, CharacterManager, App, SaveManager and TrackManager rows.
        [Test]
        public void AFreshContainerHasNoPressedMissionButtonAndItsOwnBindables()
        {
            WithManager((owned, manager, character) =>
            {
                owned.LeaseRow<TrackManager>();
                var container = owned.Host("Lucid debug container", false).AddComponent<UIContainerDebug>();

                Assert.That(Get<int>(container, "m_pressedMissionButtonIndex"), Is.EqualTo(-1));
                Assert.That(container.ChallengeXP, Is.Not.Null);
                Assert.That(container.ChallengeCycleLabel, Is.Not.Null);
                Assert.That(container.ChallengeCycleLabel, Is.Not.SameAs(container.ChallengeXP));
                Assert.That(Get<SystemRef<CharacterManager>>(container, "m_characterManagerRef").GetSafe(), Is.SameAs(manager));
                Assert.That(Get<SystemRef<App>>(container, "m_appRef").GetSafe(), Is.SameAs(ProcessManager.GetSystem<App>()));
                Assert.That(Get<SystemRef<SaveManager>>(container, "m_saveManagerRef").GetSafe(), Is.SameAs(ProcessManager.GetSystem<SaveManager>()));
            });
        }

        // GetNextRespawnPoint 0x62D6D0 returns the point after the first whose transform is the given one (0x62d899..0x62d8a2, then the
        // index test against the count at 0x62d783); the last point's own transform ends the scan and gives the first point.
        [Test]
        public void TheNextRespawnPointFollowsTheListAndWrapsToTheFirst()
        {
            Owned.Run(owned =>
            {
                LevelRespawnPoints list = owned.Register(new LevelRespawnPoints());
                RespawnPoint[] points = NewPoints(owned, list, 3);

                Assert.That(Next(points[0].transform), Is.SameAs(points[1]));
                Assert.That(Next(points[1].transform), Is.SameAs(points[2]));
                Assert.That(Next(points[2].transform), Is.SameAs(points[0]), "the last point is followed by the first");
            });
        }

        [Test]
        public void AnUnknownOrMissingTransformGivesTheFirstRespawnPoint()
        {
            Owned.Run(owned =>
            {
                LevelRespawnPoints list = owned.Register(new LevelRespawnPoints());
                RespawnPoint[] points = NewPoints(owned, list, 3);

                Assert.That(Next(owned.Host("Lucid unrelated object", false).transform), Is.SameAs(points[0]));
                Assert.That(Next(null), Is.SameAs(points[0]));
            });
        }

        // The scan stops at the first point on the given transform; a later point on the same transform is never reached, where a scan
        // that kept the last match would wrap to the first point.
        [Test]
        public void TheFirstPointOnATransformDecidesTheNextOne()
        {
            Owned.Run(owned =>
            {
                LevelRespawnPoints list = owned.Register(new LevelRespawnPoints());
                RespawnPoint[] points = NewPoints(owned, list, 2);
                RespawnPoint sharing = NewPoint(owned, null, points[0].gameObject);
                list.Add(sharing);

                Assert.That(Next(points[0].transform), Is.SameAs(points[1]));
                Assert.That(Next(sharing.transform), Is.SameAs(points[1]));
            });
        }

        [Test]
        public void AnEmptyRespawnPointListFaultsAskingForTheFirstPoint()
        {
            Owned.Run(owned =>
            {
                owned.Register(new LevelRespawnPoints());
                Assert.Throws<ArgumentOutOfRangeException>(() => Next(null));
            });
        }

        private sealed class Respawn
        {
            public Owned Owned;
            public CharacterManager Manager;
            public Character Character;
            public UIContainerDebug Container;
            public UIWidgetButton Button;
            public Toggle Invincibility;
            public RespawnPoint[] Points;
        }

        private static void WithRespawn(Action<Respawn> body) => WithManager((owned, manager, character) =>
        {
            // The container's field initialisers cache the TrackManager row; the others are registered by the fixture.
            owned.LeaseRow<TrackManager>();
            LevelRespawnPoints list = owned.Register(new LevelRespawnPoints());
            var respawn = new Respawn { Owned = owned, Manager = manager, Character = character, Points = NewPoints(owned, list, 3) };
            respawn.Button = owned.Host("Lucid respawn button", false).AddComponent<UIWidgetButton>();
            respawn.Invincibility = owned.Host("Lucid invincibility toggle", false).AddComponent<Toggle>();
            respawn.Container = owned.Host("Lucid debug container", false).AddComponent<UIContainerDebug>();
            SetSerialized(respawn.Container, "m_respawnButton", property => property.objectReferenceValue = respawn.Button);
            SetSerialized(respawn.Container, "m_invincibilityToggle", property => property.objectReferenceValue = respawn.Invincibility);
            body(respawn);
        });

        // OnCharacterManagerValid 0x62BC40 does nothing while the manager has no current character. With one, the invincibility toggle is
        // shown set to the character's DebugInvulnerable (false here), the respawn button is shown, and SetRespawnButton 0x62BCD0 labels it
        // "Respawn: " plus the name of the first respawn point (no point has been used yet).
        [Test]
        public void ACurrentCharacterRevealsTheInvincibilityToggleAndOffersTheFirstRespawnPoint()
        {
            WithRespawn(respawn =>
            {
                SetCurrent(respawn.Manager, respawn.Character, true);
                respawn.Invincibility.SetIsOnWithoutNotify(true);
                Assert.That(respawn.Character.DebugInvulnerable(), Is.False);

                Call(respawn.Container, "OnCharacterManagerValid", respawn.Manager);

                Assert.That(respawn.Invincibility.gameObject.activeSelf, Is.True);
                Assert.That(respawn.Invincibility.isOn, Is.False);
                Assert.That(respawn.Button.gameObject.activeSelf, Is.True);
                Assert.That(respawn.Button.Label.Value, Is.EqualTo("Respawn: Lucid respawn 0"));
            });
        }

        [Test]
        public void WithoutACurrentCharacterTheRespawnControlsStayHidden()
        {
            WithRespawn(respawn =>
            {
                SetCurrent(respawn.Manager, respawn.Character, false);

                Call(respawn.Container, "OnCharacterManagerValid", respawn.Manager);

                Assert.That(respawn.Invincibility.gameObject.activeSelf, Is.False);
                Assert.That(respawn.Button.gameObject.activeSelf, Is.False);
                Assert.That(respawn.Button.Label.Value, Is.Null);
            });
        }

        private static T Stored<T>(Character character, GraphStorageKey key, T missing) => character.Storage.GetValue(key, missing, false);

        // Action_OnRespawnRequest 0x62D9C0 moves the character to the point after its last one (the first when it has none), keeps no
        // velocity, requires a teleport and marks it out of bounds, then relabels the button for the point after that. The button's
        // own press (Action_OnButtonPress) runs it.
        [Test]
        public void PressingTheRespawnButtonStepsTheCharacterThroughThePoints()
        {
            WithRespawn(respawn =>
            {
                SetCurrent(respawn.Manager, respawn.Character, true);
                Call(respawn.Container, "OnCharacterManagerValid", respawn.Manager);
                Character character = respawn.Character;
                string[] expectedLabels = { "Respawn: Lucid respawn 1", "Respawn: Lucid respawn 2", "Respawn: Lucid respawn 0", "Respawn: Lucid respawn 1" };
                int[] expectedPoints = { 0, 1, 2, 0 };

                for (int press = 0; press < 4; press++)
                {
                    respawn.Button.Action_OnButtonPress();

                    Assert.That(Stored<Transform>(character, ActorFSMKeys.LastRespawnPoint, null), Is.SameAs(respawn.Points[expectedPoints[press]].transform), "press " + press);
                    Assert.That(Stored(character, ActorFSMKeys.RespawnRetainVelocity, true), Is.False, "press " + press);
                    Assert.That(Stored(character, ActorFSMKeys.RespawnTeleportRequired, false), Is.True, "press " + press);
                    Assert.That(Stored(character, ActorFSMKeys.IsOutOfBounds, false), Is.True, "press " + press);
                    Assert.That(respawn.Button.Label.Value, Is.EqualTo(expectedLabels[press]), "press " + press);
                }
            });
        }

        // Action_ToggleInvincibility 0x62D590 looks the current character up and toggles nothing (the call it made is compiled out).
        [Test]
        public void ToggleInvincibilityOnlyLooksTheCharacterUp()
        {
            WithRespawn(respawn =>
            {
                SetCurrent(respawn.Manager, respawn.Character, true);

                Assert.DoesNotThrow(() => respawn.Container.Action_ToggleInvincibility(true));

                Assert.That(respawn.Character.DebugInvulnerable(), Is.False);
            });
        }

        // Action_OnMissionTimerToggle 0x62D690 stores its argument in MissionTimer's static unlimited-time flag (0x62d6c5).
        [Test]
        public void TheMissionTimerToggleSetsTheUnlimitedTimeFlag()
        {
            WithRespawn(respawn =>
            {
                bool prior = MissionTimer.UnlimitedTime;
                try
                {
                    respawn.Container.Action_OnMissionTimerToggle(!prior);
                    Assert.That(MissionTimer.UnlimitedTime, Is.EqualTo(!prior));
                    respawn.Container.Action_OnMissionTimerToggle(prior);
                    Assert.That(MissionTimer.UnlimitedTime, Is.EqualTo(prior));
                }
                finally
                {
                    MissionTimer.Debug_SetUnlimitedTime(prior);
                }
            });
        }

        // CanSetupForMissionSelect 0x62CE30 returns false at once when the scene holds no UIContainerMissionSelect.
        [Test]
        public void WithoutAMissionSelectContainerNothingCanBeSetUpForIt()
        {
            WithRespawn(respawn =>
            {
                Assert.That(Call(respawn.Container, "CanSetupForMissionSelect"), Is.EqualTo(false));
            });
        }

        private sealed class MissionButtons
        {
            public Owned Owned;
            public OriginalPersistentLevelObjectTests.Missions Missions;
            public App App;
            public UIContainerDebug Container;
            public Transform Root;
            public UIWidgetButton Prefab;
            public MissionDefinition Definition;
            public MissionState State;

            public UIWidgetButton[] Buttons => Root.GetComponentsInChildren<UIWidgetButton>(true);
        }

        // A container over a real mission manager with a registered App. The root and the prefab have RectTransforms, as the container casts
        // its children to them. A mission is made active only when asked, which loads its state into the manager (and its level into the save).
        private static void WithMissionButtons(bool activate, Action<MissionButtons> body) =>
            OriginalPersistentLevelObjectTests.WithMissions(true, (owned, missions) =>
            {
                // Rows the App, the container, the mission definition, state and context and the tracker's field initialisers cache.
                owned.LeaseRow<UIScreenTransitionManager>();
                owned.LeaseRow<UINavigationManager>();
                owned.LeaseRow<TrackManager>();
                owned.LeaseRow<CollectableManager>();
                owned.LeaseRow<TimeTrialLeaderboardManager>();
                owned.LeaseRow<UIManager>();
                owned.LeaseRow<ChallengeManager>();
                var rig = new MissionButtons { Owned = owned, Missions = missions };
                rig.App = owned.Register(owned.Host("Lucid app", false).AddComponent<App>());
                rig.Definition = owned.Track(ScriptableObject.CreateInstance<MissionDefinition>());
                rig.Definition.name = MissionName;
                Set(rig.Definition, "m_guid", MissionGuid); // Reflection: authored GUID of a serialized definition
                Set(rig.Definition, "m_subMissions", new List<MissionDefinition>()); // Reflection: authored (empty) list
                if (activate)
                {
                    missions.Manager.SetActiveMissionContext(new MissionContextRegular(rig.Definition, missions.CurrentLevel.LevelDefinition, false));
                    rig.State = missions.Manager.MissionStates[MissionGuid];
                }
                GameObject root = owned.Track(new GameObject("Lucid mission buttons", typeof(RectTransform)));
                root.SetActive(false);
                rig.Root = root.transform;
                GameObject prefab = owned.Track(new GameObject("Lucid mission button prefab", typeof(RectTransform)));
                prefab.SetActive(false);
                rig.Prefab = prefab.AddComponent<UIWidgetButton>();
                var navigation = owned.Host("Lucid container navigation", false).AddComponent<UIContainerNavigation>();
                rig.Container = owned.Host("Lucid debug container", false).AddComponent<UIContainerDebug>();
                SetSerialized(rig.Container, "m_missionCompleteRoot", property => property.objectReferenceValue = rig.Root);
                SetSerialized(rig.Container, "m_missionCompleteButtonPrefab", property => property.objectReferenceValue = rig.Prefab);
                SetSerialized(rig.Container, "m_containerNavigation", property => property.objectReferenceValue = navigation);
                body(rig);
            });

        private static void Rebuild(MissionButtons rig) => Call(rig.Container, "OnMissionManagerValid", rig.Missions.Manager);

        // OnMissionManagerValid 0x62BF10 offers one button for the active mission: "Complete: <name>" through the lambda, unless the
        // mission has at least two objectives still to do (Target >= 2 at 0x62c41d and Target - Progress >= 2 at 0x62c425), when
        // SetupMissionObjectiveButton labels it "<name>: <progress>/<target>" (0x62c7b0).
        [TestCase(0, 0, "Complete: Lucid debug mission", TestName = "AMissionWithoutAnObjectiveTargetOffersTheCompleteButton")]
        [TestCase(1, 0, "Complete: Lucid debug mission", TestName = "ASingleObjectiveMissionOffersTheCompleteButton")]
        [TestCase(2, 1, "Complete: Lucid debug mission", TestName = "AMissionOneObjectiveFromDoneOffersTheCompleteButton")]
        [TestCase(3, 2, "Complete: Lucid debug mission", TestName = "AMissionOneObjectiveShortOfThreeOffersTheCompleteButton")]
        [TestCase(2, 0, "Lucid debug mission: 0/2", TestName = "AMissionWithTwoObjectivesLeftOffersTheObjectiveButton")]
        [TestCase(3, 1, "Lucid debug mission: 1/3", TestName = "AMissionWithTwoOfThreeObjectivesLeftOffersTheObjectiveButton")]
        [TestCase(4, 2, "Lucid debug mission: 2/4", TestName = "AMissionHalfwayThroughFourOffersTheObjectiveButton")]
        public void TheActiveMissionGetsOneButtonOfTheKindItsProgressCalls(int target, int progress, string label)
        {
            WithMissionButtons(true, rig =>
            {
                rig.State.UpdateMissionProgress(progress, target, false);

                Rebuild(rig);

                UIWidgetButton[] buttons = rig.Buttons;
                Assert.That(buttons.Length, Is.EqualTo(1));
                Assert.That(buttons[0].Label.Value, Is.EqualTo(label));
                Assert.That(buttons[0].transform.parent, Is.SameAs(rig.Root));
            });
        }

        // Without an active mission the manager has no state to offer; the old children are still closed first.
        [Test]
        public void NothingIsOfferedWithoutAnActiveMissionButTheOldButtonsAreClosed()
        {
            WithMissionButtons(false, rig =>
            {
                for (int index = 0; index < 2; index++)
                    new GameObject("Lucid stale button " + index, typeof(RectTransform)).transform.SetParent(rig.Root, false);
                try
                {
                    LogAssert.Expect(LogType.Error, RefusedDestroy);
                    LogAssert.Expect(LogType.Error, RefusedDestroy);

                    Rebuild(rig);

                    Assert.That(rig.Buttons, Is.Empty);
                }
                finally
                {
                    for (int index = rig.Root.childCount - 1; index >= 0; index--) Object.DestroyImmediate(rig.Root.GetChild(index).gameObject);
                }
            });
        }

        // SetupMissionObjectiveButton 0x62C6C0: "{0}: {1}/{2}" of the definition's name, the state's progress and its target; the
        // returned action is the local function over the same state.
        [Test]
        public void TheObjectiveButtonLabelNamesTheMissionAndItsProgress()
        {
            WithMissionButtons(true, rig =>
            {
                rig.State.UpdateMissionProgress(1, 3, false);

                Action callback = rig.Container.SetupMissionObjectiveButton(rig.State, out string label, 5);

                Assert.That(label, Is.EqualTo("Lucid debug mission: 1/3"));
                Assert.That(callback, Is.Not.Null);
                Assert.That(callback.Target.GetType().Name, Is.EqualTo("<>c__DisplayClass44_0"));
            });
        }

        // The callback 0x62E960 returns at once for a state with no instanced tracker, before the pressed index is set.
        [Test]
        public void TheObjectiveButtonDoesNothingForAStateWithoutAnInstancedTracker()
        {
            WithMissionButtons(true, rig =>
            {
                rig.State.UpdateMissionProgress(0, 3, false);
                Action callback = rig.Container.SetupMissionObjectiveButton(rig.State, out string _, 5);

                Assert.DoesNotThrow(() => callback());

                Assert.That(Get<int>(rig.Container, "m_pressedMissionButtonIndex"), Is.EqualTo(-1));
            });
        }

        private static MissionInstancedTracker InstallTracker(MissionButtons rig, int objectives, out MissionObjective[] list)
        {
            var tracker = rig.Owned.Host("Lucid mission tracker", false).AddComponent<MissionInstancedTracker>();
            list = Enumerable.Range(0, objectives).Select(_ =>
            {
                MissionObjective objective = EnemyAbilityTestSupport.AddTimeScaled<MissionObjective>(rig.Owned.Host("Lucid mission objective", false, tracker.transform));
                Author(objective, "m_onActivated"); // Reflection: authored events, which the constructor leaves null
                Author(objective, "m_onPreviouslyCompleted");
                Author(objective, "m_onReset");
                Author(objective, "m_onComplete");
                return objective;
            }).ToArray();
            Set(tracker, "m_missionObjectives", new List<MissionObjective>(list)); // Reflection: authored list (the editor Reset finds none on inactive hosts)
            // The state's tracker is installed by the addressable callback of SetUpTracker; the setter is private.
            typeof(MissionState).GetProperty("Tracker", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .GetSetMethod(true).Invoke(rig.State, new object[] { tracker });
            return tracker;
        }

        // The objective button's callback completes the tracker's first incomplete objective (the loop leaves at once, 0x62ec28 and the
        // finally), records the button's index and rebuilds every button, closing the old one first.
        [Test]
        public void PressingTheObjectiveButtonCompletesOnlyTheFirstIncompleteObjectiveAndRebuilds()
        {
            WithMissionButtons(true, rig =>
            {
                rig.State.UpdateMissionProgress(0, 3, false);
                InstallTracker(rig, 3, out MissionObjective[] objectives);
                objectives[0].Action_ObjectiveComplete();
                Rebuild(rig);
                UIWidgetButton button = rig.Buttons.Single();
                Assert.That(button.Label.Value, Is.EqualTo("Lucid debug mission: 0/3"));

                LogAssert.Expect(LogType.Error, RefusedDestroy);
                button.Action_OnButtonPress();

                Assert.That(objectives.Select(objective => objective.IsComplete).ToArray(), Is.EqualTo(new[] { true, true, false }));
                Assert.That(Get<int>(rig.Container, "m_pressedMissionButtonIndex"), Is.EqualTo(0));
                Assert.That(rig.Buttons.Length, Is.EqualTo(2), "the refused destroy leaves the old button beside the rebuilt one");
            });
        }

        // The complete button's lambda 0x62E3C0 locks the button, then adds the tracker's MissionObjectivesCompleted to the App's
        // pause-close event (the stored value is read, combined and stored again).
        [Test]
        public void PressingTheCompleteButtonQueuesTheTrackersCompletionForThePauseClose()
        {
            WithMissionButtons(true, rig =>
            {
                rig.State.UpdateMissionProgress(0, 1, false);
                MissionInstancedTracker tracker = InstallTracker(rig, 0, out MissionObjective[] _);
                Rebuild(rig);
                UIWidgetButton button = rig.Buttons.Single();
                Assert.That(button.Label.Value, Is.EqualTo("Complete: Lucid debug mission"));
                Assert.That(rig.App.Storage.GetValue<Action>(AppFSMKeys.OnPauseCloseEvent, null, false), Is.Null);

                button.Action_OnButtonPress();

                Delegate[] queued = rig.App.Storage.GetValue<Action>(AppFSMKeys.OnPauseCloseEvent, null, false).GetInvocationList();
                Assert.That(queued.Length, Is.EqualTo(1));
                Assert.That(queued[0].Target, Is.SameAs(tracker));
                Assert.That(queued[0].Method.Name, Is.EqualTo("MissionObjectivesCompleted"));
            });
        }

        private static MissionRank NewRank(RankType type, int seconds)
        {
            var rank = new MissionRank();
            JsonUtility.FromJsonOverwrite("{\"m_rankType\":" + (int)type + ",\"m_seconds\":" + seconds + "}", rank);
            rank.Validate();
            return rank;
        }

        // SetupForMission finds its definition through MissionManager.GetAllMissionDefinitions: the mission is listed by a level with
        // ShowInLevelSelect in the LevelManager's GameLevels (one zone holding it). Ranks S 10 s and A 20 s are added when asked.
        // Returns the save record of the mission, in the level's saved data.
        private static SaveDataLevelMission ListMission(MissionButtons rig, bool ranked)
        {
            Owned owned = rig.Owned;
            owned.LeaseRow<DataManager>(); // the rank's field initialiser
            if (ranked)
            {
                var ranks = owned.Track(ScriptableObject.CreateInstance<MissionRankDefinition>());
                ranks.Ranks.Add(RankType.S, NewRank(RankType.S, 10));
                ranks.Ranks.Add(RankType.A, NewRank(RankType.A, 20));
                Call(ranks, "OnEnable"); // the sorted list built from the ranks just added
                Set(rig.Definition, "m_ranks", ranks); // Reflection: serialized reference
            }
            var group = new MissionGroup();
            Set(group, "m_definitions", new List<MissionDefinition> { rig.Definition }); // Reflection: authored group contents
            Set(group, "m_requirements", new List<RequirementMissionGroupBase>());
            var missionList = owned.Track(ScriptableObject.CreateInstance<MissionList>());
            Set(missionList, "m_groups", new List<MissionGroup> { group });
            var level = owned.Track(ScriptableObject.CreateInstance<GameplayLevelDefinition>());
            Set(level, "m_guid", "lucid-debug-container-level");
            Set(level, "m_missionList", missionList);
            var zone = owned.Track(ScriptableObject.CreateInstance<GameplayLevelCategory>());
            Set(zone, "m_guid", "lucid-debug-container-zone");
            Set(zone, "m_gameplayEntries", new List<GameplayLevelUIEntry> { level });
            var levels = owned.Track(ScriptableObject.CreateInstance<GameLevels>());
            ((List<GameplayLevelUIEntry>)typeof(GameLevels).GetField("m_gameplayEntries", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(levels)).Add(zone);
            typeof(LevelManager).GetProperty("GameLevels").SetValue(rig.Missions.Level, levels); // the property is set by the LevelManager's Awake
            return rig.Missions.Save.CurrentSave.GetOrCreateLevelData("lucid-debug-container-level").GetOrCreateMissionData(MissionGuid);
        }

        // SetupForMission 0x62D180 offers no button for a mission the manager does not list (0x62d2ef).
        [Test]
        public void NoButtonIsOfferedForAMissionTheManagerDoesNotKnow()
        {
            WithMissionButtons(false, rig =>
            {
                ListMission(rig, true);

                Assert.That(Call(rig.Container, "SetupForMission", new SaveDataLevelMission("lucid-unlisted-mission")), Is.EqualTo(false));
                Assert.That(rig.Buttons, Is.Empty);
            });
        }

        // A completed mission whose best time earns no rank gets no button (0x62d318); with ranks S 10 s and A 20 s, 25 s is past both
        // and there is no D rank. A completed mission already ranked S gets none either (the literal 0xDF9F103C at 0x62d322, which is
        // not A: 15 s earns A and does get a button).
        [TestCase(25f, false, TestName = "ACompletedMissionWithAnUnrankedBestTimeGetsNoButton")]
        [TestCase(5f, false, TestName = "ACompletedMissionRankedSGetsNoButton")]
        [TestCase(15f, true, TestName = "ACompletedMissionRankedAGetsAButton")]
        public void ACompletedMissionIsOfferedAButtonUnlessItsBestTimeEarnsNoRankOrS(float bestTime, bool offered)
        {
            WithMissionButtons(false, rig =>
            {
                SaveDataLevelMission save = ListMission(rig, true);
                save.BestTimeSeconds = bestTime;
                save.MarkComplete(1);

                Assert.That(Call(rig.Container, "SetupForMission", save), Is.EqualTo(offered));
                Assert.That(rig.Buttons.Length, Is.EqualTo(offered ? 1 : 0));
            });
        }

        // An incomplete mission is never ranked: even a best time that would earn S gets a button, labelled with the mission name.
        [Test]
        public void AnIncompleteMissionIsOfferedACompleteMissionButton()
        {
            WithMissionButtons(false, rig =>
            {
                SaveDataLevelMission save = ListMission(rig, true);
                save.BestTimeSeconds = 5f;

                Assert.That(Call(rig.Container, "SetupForMission", save), Is.EqualTo(true));

                UIWidgetButton button = rig.Buttons.Single();
                Assert.That(button.gameObject.activeSelf, Is.True);
                Assert.That(button.Label.Value, Is.EqualTo("Complete mission: Lucid debug mission"));
                Assert.That(button.transform.parent, Is.SameAs(rig.Root));
            });
        }

        // The button's lambda 0x62E760 sets the best time to the best rank's 10 s less 0.1 (an addition of -0.1f, 0xbdcccccd at
        // 0x29a70d4, rounded to single: 9.9f), marks the mission complete with progress 1 (0x62e816), destroys the button (refused in
        // Edit Mode) and offers the next mission, of which there is none. A mission without ranks keeps its best time.
        [TestCase(true, 9.9f, TestName = "PressingTheMissionButtonSetsTheBestTimeJustUnderTheBestRank")]
        [TestCase(false, 5f, TestName = "PressingTheButtonOfAMissionWithoutRanksKeepsTheBestTime")]
        public void PressingTheMissionButtonCompletesTheMission(bool ranked, float expectedBestTime)
        {
            WithMissionButtons(false, rig =>
            {
                SaveDataLevelMission save = ListMission(rig, ranked);
                save.BestTimeSeconds = 5f;
                Call(rig.Container, "SetupForMission", save);
                UIWidgetButton button = rig.Buttons.Single();

                LogAssert.Expect(LogType.Error, RefusedDestroy);
                button.Action_OnButtonPress();

                Assert.That(save.BestTimeSeconds, Is.EqualTo(expectedBestTime));
                Assert.That(save.Complete, Is.True);
                Assert.That(save.Progress, Is.EqualTo(1));
            });
        }
    }
}
