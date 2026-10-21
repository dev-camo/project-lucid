using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.MissionTrackerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerLevelResults (Game.Runtime 0x020008c7; UpdateTimeTrialBindings 0x6370A0, SetupActiveMissionUI 0x637260,
    // SetupChallenge 0x6374C0, SetupLeaderboards 0x637740, GetActiveMissionIdentifier 0x6377B0, Action_Exit 0x637820, Exit
    // 0x637830, PublishUIMessage 0x637B60, constructor 0x637C30) over a real mission manager, mission states with real
    // instanced trackers, a real App and character manager (for Exit) and identifiers whose containers are already loaded.
    // Not covered: OnEnable (it also reads the data manager's level lookup, the ranks and results containers, the level
    // manager's zones and the App's storage), SetupZone (the level manager's zone table and an Addressables sprite load),
    // the challenge branch of SetupChallenge (a challenge context with its results container), OnQualityManagerValid (a
    // VisualQualityManager_SDT), Restart and Action_Restart (LevelManager.Restart) and OnDestroy's release of a loaded zone
    // accent.
    public sealed class UIContainerLevelResultsTests
    {
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");

        private static void Run(Action<World, UIContainerLevelResults> body) => WithWorld(world =>
        {
            world.Owned.LeaseRow<App>(); // the active mission container's field initialiser
            body(world, world.Owned.Host("Lucid level results", false).AddComponent<UIContainerLevelResults>());
        });

        private static MissionState NewTrackedState(World world, MissionTracker.FailStateBehaviourType fail = MissionTracker.FailStateBehaviourType.None, UIContainerIdentifier resultsOverride = null)
        {
            Mission mission = world.NewMission();
            var tracker = world.NewTracker<MissionInstancedTracker>();
            Set(tracker, "m_missionObjectives", new List<MissionObjective>()); // Reflection: authored (empty) objective list
            if (resultsOverride != null) Set(tracker, "m_resultsActiveMissionOverrideIdentifier", resultsOverride); // Reflection: serialized reference
            Call(mission.State, "OnTrackerSetup", tracker, null, null, true);
            return mission.State;
        }

        private static UIContainerIdentifier NewIdentifier(Owned owned, UIContainer loaded)
        {
            var identifier = owned.Track(ScriptableObject.CreateInstance<UIContainerIdentifier>());
            identifier.RegisterContainer(loaded);
            return identifier;
        }

        // dump.cs TypeDefIndex 2246 and the DummyDll rows (fields 0x040022fd..0x04002316).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerLevelResults), 0x00100101,
                new (string, int)[]
                {
                    ("m_exitEvent", 0x0001), ("m_ranksContainer", 0x0001), ("m_resultsContainer", 0x0001), ("m_resultsSecondaryContainer", 0x0001),
                    ("m_defaultActiveMissionIdentifier", 0x0001), ("m_activeMissionRoot", 0x0001), ("m_challengeResultsContainerIdentifier", 0x0001),
                    ("m_challengeResultsRoot", 0x0001), ("m_collectableCounter", 0x0001), ("LevelTitle", 0x0026), ("LevelSubtitle", 0x0026),
                    ("CanReplayMission", 0x0026), ("ShowFPSWarningMessage", 0x0026), ("ZoneIconAsset", 0x0026), ("IsChallenge", 0x0026),
                    ("TimeTrialUnlocked", 0x0026), ("TimeTrialMissionName", 0x0026), ("ShowLeaderboards", 0x0026), ("LeaderboardIdentifier", 0x0026),
                    ("ShowMoreGamesButton", 0x0026), ("ExtraSpaceAboveXPReward", 0x0026), ("m_missionManagerRef", 0x0021), ("m_levelManagerRef", 0x0021),
                    ("m_challengeResultsContainer", 0x0001), ("m_zoneAccent", 0x0001), ("m_isExiting", 0x0001),
                },
                new (string, int)[]
                {
                    ("OnEnable", 0x0081), ("UpdateTimeTrialBindings", 0x0081), ("SetupActiveMissionUI", 0x0081), ("SetupZone", 0x0081),
                    ("SetupChallenge", 0x0081), ("SetupLeaderboards", 0x0081), ("GetActiveMissionIdentifier", 0x0081), ("OnQualityManagerValid", 0x0081),
                    ("Action_Exit", 0x0086), ("Action_Restart", 0x0086), ("Exit", 0x0081), ("Restart", 0x0081), ("PublishUIMessage", 0x0091),
                    ("OnDestroy", 0x0081), (".ctor", 0x1886),
                });
        }

        // GetActiveMissionIdentifier 0x6377B0: the tracker's results override when it names one (op_Inequality at 0x6377C9),
        // otherwise the container's default.
        [Test]
        public void TheTrackersResultsOverrideReplacesTheDefaultActiveMissionIdentifier()
        {
            Run((world, container) =>
            {
                var fallback = world.Owned.Track(ScriptableObject.CreateInstance<UIContainerIdentifier>());
                var custom = world.Owned.Track(ScriptableObject.CreateInstance<UIContainerIdentifier>());
                Set(container, "m_defaultActiveMissionIdentifier", fallback); // Reflection: serialized reference

                Assert.That(Call(container, "GetActiveMissionIdentifier", NewTrackedState(world)), Is.SameAs(fallback));
                Assert.That(Call(container, "GetActiveMissionIdentifier", NewTrackedState(world, resultsOverride: custom)), Is.SameAs(custom));
            });
        }

        // SetupActiveMissionUI 0x637260: a mission can be replayed when it is a challenge, is not complete yet, or is a complete
        // time trial (the or at 0x6372D4 of isChallenge with !Complete || IsTimeTrial). The identifier's container is not an active
        // mission container here, so the identifier is closed (the jump to Close at 0x637334).
        [Test]
        public void AMissionCanBeReplayedWhenItIsAChallengeIncompleteOrACompleteTimeTrial()
        {
            // (complete, time trial, challenge) -> can replay
            foreach ((bool complete, bool timeTrial, bool challenge, bool expected) in new[]
            {
                (false, false, false, true),
                (false, false, true, true),
                (true, false, false, false),
                (true, false, true, true),
                (true, true, false, true),
            })
            {
                Run((world, container) =>
                {
                    MissionState state = NewTrackedState(world);
                    Get<SaveDataLevelMission>(state, "m_saveData").Complete = complete; // Reflection: the record SetUpData was given
                    if (timeTrial)
                    {
                        Set(state.Definition, "m_timeLimitSeconds", 60); // Reflection: authored mission data
                        Set(state.Definition, "m_isBeingOverridden", true); // Reflection: set by UpdateOverrides for a completed mission with overrides
                    }
                    var foreign = world.Owned.Host("Lucid foreign container", false).AddComponent<UIContainerChallengeResults>();
                    UIContainerIdentifier identifier = NewIdentifier(world.Owned, foreign);
                    Set(container, "m_defaultActiveMissionIdentifier", identifier); // Reflection: serialized reference
                    string row = "complete " + complete + ", time trial " + timeTrial + ", challenge " + challenge;

                    Call(container, "SetupActiveMissionUI", state, challenge);

                    Assert.That(container.CanReplayMission.Value, Is.EqualTo(expected), row);
                    Assert.That(Get<int>(identifier, "m_refCount"), Is.EqualTo(1), row + ": registered, fetched and closed");
                });
            }
        }

        // With an active mission container loaded, a copy is instantiated under the active mission root with its rank audio switched
        // on (SetRankAudioActive, tail-called at 0x637377) and the identifier stays open.
        [Test]
        public void AnActiveMissionContainerIsCopiedUnderTheRootWithItsRankAudioOn()
        {
            Run((world, container) =>
            {
                var prefab = world.Owned.Host("Lucid active mission prefab", false).AddComponent<UIContainerActiveMission>();
                UIContainerIdentifier identifier = NewIdentifier(world.Owned, prefab);
                Transform root = world.Owned.Host("Lucid active mission root", false).transform;
                Set(container, "m_defaultActiveMissionIdentifier", identifier); // Reflection: serialized references
                Set(container, "m_activeMissionRoot", root);

                Call(container, "SetupActiveMissionUI", NewTrackedState(world), false);

                Assert.That(root.childCount, Is.EqualTo(1));
                var copy = root.GetChild(0).GetComponent<UIContainerActiveMission>();
                Assert.That(copy, Is.Not.SameAs(prefab));
                Assert.That(Get<bool>(copy, "m_shouldPlayRankAudio"), Is.True);
                Assert.That(Get<bool>(prefab, "m_shouldPlayRankAudio"), Is.False, "the loaded container itself is untouched");
                Assert.That(Get<int>(identifier, "m_refCount"), Is.EqualTo(2), "registered and fetched; not closed");
            });
        }

        // SetupLeaderboards 0x637740: a mission without a leaderboard identifier hides the leaderboards and writes no identifier.
        [Test]
        public void AMissionWithoutALeaderboardHidesTheLeaderboards()
        {
            Run((world, container) =>
            {
                container.ShowLeaderboards.Value = true;
                container.LeaderboardIdentifier.Value = LeaderboardIdentifier.TailsChallenges_Daily;
                MissionState state = NewTrackedState(world);

                Call(container, "SetupLeaderboards", state.Definition);

                Assert.That(container.ShowLeaderboards.Value, Is.False);
                Assert.That(container.LeaderboardIdentifier.Value, Is.EqualTo(LeaderboardIdentifier.TailsChallenges_Daily), "left as it was");
            });
        }

        // SetupChallenge 0x6374C0 outside a challenge: the extra space is cleared and the challenge flag is false (0x63756F); the
        // more-games button and the results container are untouched.
        [Test]
        public void OutsideAChallengeTheChallengeFlagsAreCleared()
        {
            Run((world, container) =>
            {
                container.IsChallenge.Value = true;
                container.ExtraSpaceAboveXPReward.Value = true;
                MissionState state = NewTrackedState(world);
                Assert.That(world.Missions.ActiveMissionContext, Is.Null);

                Call(container, "SetupChallenge", world.Missions, state.Tracker);

                Assert.That(container.IsChallenge.Value, Is.False);
                Assert.That(container.ExtraSpaceAboveXPReward.Value, Is.False);
                Assert.That(container.ShowMoreGamesButton.Value, Is.False);
                Assert.That(Get<UIContainerChallengeResults>(container, "m_challengeResultsContainer"), Is.Null);
            });
        }

        // UpdateTimeTrialBindings 0x6370A0 with no regular mission context: the unlock flag is cleared and nothing else happens.
        [Test]
        public void WithoutARegularContextNoTimeTrialIsAnnounced()
        {
            Run((world, container) =>
            {
                container.TimeTrialUnlocked.Value = true;
                container.TimeTrialMissionName.Value = "stale";

                Call(container, "UpdateTimeTrialBindings", world.Missions, world.Owned.Track(ScriptableObject.CreateInstance<GameplayLevelDefinition>()));

                Assert.That(container.TimeTrialUnlocked.Value, Is.False);
                Assert.That(container.TimeTrialMissionName.Value, Is.EqualTo("stale"));
            });
        }

        private static void WithRegularContext(string missionJson, Action<UIContainerLevelResults, OriginalPersistentLevelObjectTests.Missions, MissionDefinition, GameplayLevelDefinition> body) =>
            OriginalPersistentLevelObjectTests.WithMissions(false, (owned, missions) =>
            {
                owned.LeaseRow<TimeTrialLeaderboardManager>(); // MissionDefinition's field initialiser
                owned.LeaseRow<UIManager>(); // MissionState's field initialiser
                owned.LeaseRow<ChallengeManager>(); // MissionContextRegular's field initialiser
                var mission = ChallengeRewardTestSupport.Create<MissionDefinition>(owned, missionJson);
                Set(mission, "m_guid", "lucid-time-trial-mission"); // Reflection: authored GUID
                Set(mission, "m_subMissions", new List<MissionDefinition>()); // Reflection: authored (empty) list
                GameplayLevelDefinition level = missions.CurrentLevel.LevelDefinition;
                missions.Manager.SetActiveMissionContext(new MissionContextRegular(mission, level, false));
                body(owned.Host("Lucid level results", false).AddComponent<UIContainerLevelResults>(), missions, mission, level);
            });

        // A mission with a time limit that ends at the goal announces its time trial once (0x637188 tests the goal flag at +0xCF):
        // the unlock is shown with the context's override description (empty without a completed-mission override) and recorded
        // as seen, with a save requested; announcing it again finds the record and shows nothing (0x63714B).
        [Test]
        public void ATimeTrialIsAnnouncedOnlyOnceItsUnlockIsRecordedAsSeen()
        {
            WithRegularContext("{\"m_timeLimitSeconds\":60,\"m_isReachGoalTracker\":true}", (container, missions, mission, level) =>
            {
                container.TimeTrialMissionName.Value = "stale";

                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                Call(container, "UpdateTimeTrialBindings", missions.Manager, level);

                Assert.That(container.TimeTrialUnlocked.Value, Is.True);
                Assert.That(container.TimeTrialMissionName.Value, Is.EqualTo(string.Empty));
                Assert.That(missions.Save.CurrentSave.GetOrCreateLevelData(level.GetGUID()).GetOrCreateMissionData(mission.GetGUID()).TimeTrialUnlockSeen, Is.True);

                Call(container, "UpdateTimeTrialBindings", missions.Manager, level);

                Assert.That(container.TimeTrialUnlocked.Value, Is.False, "cleared first, then the seen record ends the call");
            });
        }

        // The goal flag is required: a timed mission that is not a reach-goal mission announces nothing and records nothing; a
        // mission without a time limit does not even read the record.
        [Test]
        public void OnlyATimedReachGoalMissionAnnouncesATimeTrial()
        {
            WithRegularContext("{\"m_timeLimitSeconds\":60}", (container, missions, mission, level) =>
            {
                Call(container, "UpdateTimeTrialBindings", missions.Manager, level);

                Assert.That(container.TimeTrialUnlocked.Value, Is.False);
                Assert.That(missions.Save.CurrentSave.GetOrCreateLevelData(level.GetGUID()).GetOrCreateMissionData(mission.GetGUID()).TimeTrialUnlockSeen, Is.False);
            });
            WithRegularContext("{\"m_isReachGoalTracker\":true}", (container, missions, mission, level) =>
            {
                Call(container, "UpdateTimeTrialBindings", missions.Manager, level);

                Assert.That(container.TimeTrialUnlocked.Value, Is.False);
                Assert.That(missions.Save.CurrentSave.GetOrCreateLevelData(level.GetGUID()).GetOrCreateMissionData(mission.GetGUID()).TimeTrialUnlockSeen, Is.False);
            });
        }

        // Exit 0x637830 with no current character publishes the exit event to the App once (PublishUIMessage 0x637B60,
        // AddUniqueUIModernEvent) and a second Exit does nothing, whatever the event (the guard at 0x637889).
        [Test]
        public void ExitPublishesTheExitEventOnlyOnce()
        {
            CharacterTriggerTestSupport.WithManager((owned, manager, character) =>
            {
                var app = ProcessManager.GetSystem<App>();
                var events = new List<UIModernEvent>();
                app.Storage.SetValue(AppFSMKeys.UIModernEvents, events);
                var first = owned.Track(ScriptableObject.CreateInstance<UIModernEvent>());
                var second = owned.Track(ScriptableObject.CreateInstance<UIModernEvent>());
                var container = owned.Host("Lucid level results", false).AddComponent<UIContainerLevelResults>();
                Set(container, "m_exitEvent", first); // Reflection: serialized reference
                character.Storage.SetValue(ActorFSMKeys.IsCollectingOrb, true);

                container.Action_Exit();
                Set(container, "m_exitEvent", second);
                container.Action_Exit();

                Assert.That(events, Is.EqualTo(new[] { first }));
                Assert.That(character.Storage.GetValue<bool>(ActorFSMKeys.IsCollectingOrb, false, true), Is.True, "no current character, so none is touched");
            });
        }

        // With a current character Exit first clears its orb-collecting flag (0x637962) and then publishes the event.
        [Test]
        public void ExitStopsTheCurrentCharacterCollectingOrbs()
        {
            CharacterTriggerTestSupport.WithManager((owned, manager, character) =>
            {
                var app = ProcessManager.GetSystem<App>();
                var events = new List<UIModernEvent>();
                app.Storage.SetValue(AppFSMKeys.UIModernEvents, events);
                var exitEvent = owned.Track(ScriptableObject.CreateInstance<UIModernEvent>());
                var container = owned.Host("Lucid level results", false).AddComponent<UIContainerLevelResults>();
                Set(container, "m_exitEvent", exitEvent); // Reflection: serialized reference
                CharacterTriggerTestSupport.SetCurrent(manager, character, true);
                character.Storage.SetValue(ActorFSMKeys.IsCollectingOrb, true);

                container.Action_Exit();

                Assert.That(character.Storage.GetValue<bool>(ActorFSMKeys.IsCollectingOrb, true, true), Is.False);
                Assert.That(events, Is.EqualTo(new[] { exitEvent }));
            });
        }
    }
}
