using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.UiPublishingFixtures;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerPause (Game.Runtime 0x020008fd; Start 0x6700F0, OnLevelManagerValid 0x6702E0, OnQualityManagerValid
    // 0x6703D0, OnMissionManagerValid 0x670440, Action_Restart 0x670870, Action_Respawn 0x670980, Restart 0x670910 and its
    // iterator 0x6718E0, Respawn 0x670A20 and its iterator 0x671420, Close 0x670D20, FireMissionQuitAnalytics 0x670C10,
    // OnDestroy 0x670DA0) as a real component on an owned inactive host. Two rigs: the mission rig (a real MissionManager,
    // LevelManager and SaveManager from OriginalPersistentLevelObjectTests.WithMissions, a genuine App, a scripted
    // UIManager and the quality services) and the character rig (CharacterTriggerTestSupport.WithManager, a real
    // CharacterManager and Character, plus the scripted UIManager).
    //
    // Not covered: Action_Exit (its LevelManager.ExitLevel needs the state LevelManager.Initialise builds), the character
    // swap wait in Respawn (the swap coroutine cannot be created without running a swap), Character.OnRestart in Restart (it
    // needs the character's collider), the challenge context branch of OnMissionManagerValid (the sealed ranks container shows
    // the same flag for a challenge without rank definitions), the final m_closingCoroutine = null of the Restart and Respawn
    // iterators (0x6719BA, 0x671838; a Coroutine cannot be constructed to seed a non-null value) and the coroutines Action_Restart and Action_Respawn start,
    // which the refusing coroutine host rejects with the engine error expected below.
    public sealed class UIContainerPauseTests
    {
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");

        private const string MissionGuid = "lucid-pause-mission";

        private sealed class MissionRig
        {
            internal Owned Owned;
            internal OriginalPersistentLevelObjectTests.Missions Missions;
            internal App App;
            internal UIManager UiManager;
            internal VisualQualityManager_SDT Quality;
            internal GameLevels Levels;
            internal UIContainerPause Container;
            internal UIContainerMissionResults Results;
            internal UIContainerMissionResults Secondary;
            internal UIContainerMissionRanks Ranks;
            internal UIModernEvent ResumeEvent;
        }

        private static UIContainerMissionResults NewResults(Owned owned)
        {
            var results = owned.Host("Lucid pause results", false).AddComponent<UIContainerMissionResults>();
            Set(results, "m_root", owned.Host("Lucid pause results root", false).AddComponent<RectTransform>()); // serialized in the original
            return results;
        }

        // The quality manager's "graphics switching" requirement is an authored level; the profile holds no attribute for it, so
        // the device can change graphics quality exactly when the required level is above Medium.
        private static void RunMissions(bool currentLevelValid, PerformanceProfile.QualityLevel requiredLevel, Action<MissionRig> body)
        {
            SettingsPageTestSupport.InitialiseAnalytics(); // the collector's type initialiser runs over owned rows here, not in whichever case reports first
            OriginalPersistentLevelObjectTests.WithMissions(currentLevelValid, (owned, missions) =>
            {
                owned.LeaseRow<TimeTrialLeaderboardManager>(); // MissionDefinition's field initialiser
                owned.LeaseRow<ChallengeManager>(); // MissionContextRegular's field initialiser
                owned.LeaseRow<UIScreenTransitionManager>(); // App's field initialiser
                var rig = new MissionRig { Owned = owned, Missions = missions };
                rig.App = owned.Register(owned.Host("Lucid pause app", false).AddComponent<App>());
                rig.App.Storage.SetValue(AppFSMKeys.UIModernEvents, new List<UIModernEvent>());
                rig.UiManager = NewUiManager(owned, out _);
                owned.Register(owned.Host("Lucid pause data manager", false).AddComponent<DataManager>());
                var performance = owned.Register(owned.Host("Lucid pause scalable performance", false).AddComponent<ScalablePerformance>());
                performance.SetPerformanceProfile(owned.Track(ScriptableObject.CreateInstance<PerformanceProfile>()));
                rig.Quality = owned.Register(owned.Host("Lucid pause quality manager", false).AddComponent<VisualQualityManager_SDT>());
                var feature = owned.Track(ScriptableObject.CreateInstance<ScalableFeature>());
                feature.ID = "lucid-pause-graphics-switching";
                Set(rig.Quality, "m_allowGraphicsQualitySwitching", new PerformanceAttribute { Feature = feature, Level = requiredLevel }); // serialized in the original
                // Awake builds it from the configuration; this one lists no zone unless a case adds one.
                rig.Levels = owned.Track(ScriptableObject.CreateInstance<GameLevels>());
                typeof(LevelManager).GetProperty("GameLevels").SetValue(missions.Level, rig.Levels); // Reflection: LevelManager.GameLevels has a private setter that its Awake calls
                rig.Container = owned.Host("Lucid pause", false).AddComponent<UIContainerPause>();
                rig.ResumeEvent = NewEvent(owned, "pause-resume");
                rig.Results = NewResults(owned);
                rig.Ranks = owned.Host("Lucid pause ranks", false).AddComponent<UIContainerMissionRanks>();
                // Serialized in the original.
                Set(rig.Container, "m_resumeEvent", rig.ResumeEvent);
                Set(rig.Container, "m_resultsContainer", rig.Results); // Reflection: serialized m_resultsContainer the editor assigns
                Set(rig.Container, "m_missionRanksContainer", rig.Ranks); // Reflection: serialized m_missionRanksContainer the editor assigns
                body(rig);
            });
        }

        private static void RunMissions(Action<MissionRig> body) => RunMissions(true, PerformanceProfile.QualityLevel.High, body);

        private static List<UIModernEvent> Events(MissionRig rig) => rig.App.GetUIModernEvents();

        // The active mission, published as the mission flow does (MissionManager.SetActiveMissionContext loads its mission).
        private static MissionDefinition StartMission(MissionRig rig)
        {
            var mission = Create<MissionDefinition>(rig.Owned);
            Set(mission, "m_guid", MissionGuid); // authored GUID of a serialized definition
            Set(mission, "m_subMissions", new List<MissionDefinition>()); // Reflection: serialized m_subMissions the editor assigns
            rig.Missions.Manager.SetActiveMissionContext(new MissionContextRegular(mission, rig.Missions.CurrentLevel.LevelDefinition, false));
            return mission;
        }

        // ---- Start and the manager callbacks --------------------------------------------------------------------------

        // Start 0x6700F0 runs the gameplay control base, then asks for the UIManager, the LevelManager and the visual quality
        // manager in that order (0x6701E0, 0x670237, 0x67028E); each is registered, so each callback runs at once. The level
        // manager's callback asks for the MissionManager, whose callback publishes the level.
        [Test]
        public void StartTakesTheManagersAndPublishesTheLevelAndTheQualityFlags()
        {
            RunMissions(rig =>
            {
                JsonUtility.FromJsonOverwrite("{\"m_displayName\":" + (int)Strings.MISSING + ",\"m_actName\":" + (int)Strings.DECIMAL_SEPARATOR + "}",
                    rig.Missions.CurrentLevel.LevelDefinition);

                Call(rig.Container, "Start");

                Assert.That(Get<UIManager>(rig.Container, "m_uiManager"), Is.SameAs(rig.UiManager));
                Assert.That(Get<LevelManager>(rig.Container, "m_levelManager"), Is.SameAs(rig.Missions.Level));
                Assert.That(Get<MissionManager>(rig.Container, "m_missionManager"), Is.SameAs(rig.Missions.Manager));
                Assert.That(rig.Container.LevelTitle.Value, Is.EqualTo(Strings.MISSING));
                Assert.That(rig.Container.LevelSubtitle.Value, Is.EqualTo(Strings.DECIMAL_SEPARATOR));
                Assert.That(rig.Container.ShowFPSWarningMessage.Value, Is.False);
                Assert.That(rig.Container.ShowFullscreenBackground.Value, Is.False);
            });
        }

        // OnQualityManagerValid 0x6703D0 publishes LevelDippedFrames unchanged (0x6703EC) and the negation of
        // DeviceCanChangeGraphicsQuality (xor al, 1 at 0x670418). All four combinations: the frame-time total decides the first
        // flag and the required quality level the second.
        [Test]
        public void TheQualityFlagsFollowTheFrameTimeAndTheDeviceCapability()
        {
            foreach ((bool dipped, PerformanceProfile.QualityLevel required, bool fullscreen) in new[]
            {
                (false, PerformanceProfile.QualityLevel.High, false),
                (true, PerformanceProfile.QualityLevel.High, false),
                (false, PerformanceProfile.QualityLevel.Medium, true),
                (true, PerformanceProfile.QualityLevel.Medium, true),
            })
            {
                RunMissions(true, required, rig =>
                {
                    // Reflection: the total of the long frames is accumulated by the manager's own update; nothing sets it.
                    Set(rig.Quality, "m_timeOverDesiredFrameTime", dipped ? 11f : 0f);

                    Call(rig.Container, "OnQualityManagerValid", rig.Quality); // Reflection: private callback that InvokeOnValid runs when the system registers

                    Assert.That(rig.Container.ShowFPSWarningMessage.Value, Is.EqualTo(dipped), "dipped " + dipped + " " + required);
                    Assert.That(rig.Container.ShowFullscreenBackground.Value, Is.EqualTo(fullscreen), "dipped " + dipped + " " + required);
                });
            }
        }

        // OnLevelManagerValid 0x6702E0 keeps the level manager and goes on to the MissionManager (0x6703B6).
        [Test]
        public void TheLevelManagerCallbackKeepsTheManagerAndAsksForTheMissionManager()
        {
            RunMissions(rig =>
            {
                Call(rig.Container, "OnLevelManagerValid", rig.Missions.Level); // Reflection: private callback that InvokeOnValid runs when the system registers

                Assert.That(Get<LevelManager>(rig.Container, "m_levelManager"), Is.SameAs(rig.Missions.Level));
                Assert.That(Get<MissionManager>(rig.Container, "m_missionManager"), Is.SameAs(rig.Missions.Manager));
            });
        }

        // ---- OnMissionManagerValid ------------------------------------------------------------------------------------

        // Without a current level (0x670531) only the manager is kept.
        [Test]
        public void WithoutACurrentLevelOnlyTheMissionManagerIsKept()
        {
            RunMissions(false, PerformanceProfile.QualityLevel.High, rig =>
            {
                Set(rig.Container, "m_levelManager", rig.Missions.Level); // Reflection: OnLevelManagerValid assigns it from the registry; the case skips that callback
                rig.Container.LevelTitle.Value = Strings.MISSING;

                Call(rig.Container, "OnMissionManagerValid", rig.Missions.Manager); // Reflection: private callback that InvokeOnValid runs when the system registers

                Assert.That(Get<MissionManager>(rig.Container, "m_missionManager"), Is.SameAs(rig.Missions.Manager));
                Assert.That(rig.Container.LevelTitle.Value, Is.EqualTo(Strings.MISSING), "not republished");
            });
        }

        // With a level but no active mission (0x670552 false, jump to 0x6706BE) the level's name (+0x20) and act name (+0x24)
        // are published, the results and ranks containers are not touched (they are unset here, so any touch would fault)
        // and a level in no zone ends the method (0x67070C).
        [Test]
        public void ALevelWithoutAMissionOrAZonePublishesOnlyItsNames()
        {
            RunMissions(rig =>
            {
                JsonUtility.FromJsonOverwrite("{\"m_displayName\":" + (int)Strings.MISSING + ",\"m_actName\":" + (int)Strings.DECIMAL_SEPARATOR + "}",
                    rig.Missions.CurrentLevel.LevelDefinition);
                Set(rig.Container, "m_levelManager", rig.Missions.Level); // Reflection: OnLevelManagerValid assigns it from the registry; the case skips that callback
                Set(rig.Container, "m_resultsContainer", null); // Reflection: serialized m_resultsContainer the editor assigns
                Set(rig.Container, "m_missionRanksContainer", null); // Reflection: serialized m_missionRanksContainer the editor assigns

                Call(rig.Container, "OnMissionManagerValid", rig.Missions.Manager); // Reflection: private callback that InvokeOnValid runs when the system registers

                Assert.That(rig.Container.LevelTitle.Value, Is.EqualTo(Strings.MISSING));
                Assert.That(rig.Container.LevelSubtitle.Value, Is.EqualTo(Strings.DECIMAL_SEPARATOR));
                Assert.That(rig.Container.DreamNumber.Value, Is.Zero);
                Assert.That(rig.Container.ZoneIconAsset.Value == null, Is.True);
                Assert.That(Get<object>(rig.Container, "m_zoneAccent"), Is.Null);
            });
        }

        // With an active mission (0x670552 true) the primary results container gets the mission definition (state +0x48) and
        // the level (0x67057D), the secondary one the same when it is set (the op_Inequality test at 0x6705D6) and the ranks
        // container is set up with the mission (0x670688), which makes it hide its ranks for a mission without any.
        [Test]
        public void AnActiveMissionIsHandedToTheResultsAndRanksContainers()
        {
            foreach (bool withSecondary in new[] { true, false })
            {
                RunMissions(rig =>
                {
                    MissionDefinition mission = StartMission(rig);
                    GameplayLevelDefinition level = rig.Missions.CurrentLevel.LevelDefinition;
                    if (withSecondary)
                    {
                        rig.Secondary = NewResults(rig.Owned);
                        Set(rig.Container, "m_resultsSecondaryContainer", rig.Secondary); // Reflection: serialized m_resultsSecondaryContainer the editor assigns
                    }
                    Set(rig.Container, "m_levelManager", rig.Missions.Level); // Reflection: OnLevelManagerValid assigns it from the registry; the case skips that callback
                    rig.Ranks.Show.Value = true;

                    Call(rig.Container, "OnMissionManagerValid", rig.Missions.Manager); // Reflection: private callback that InvokeOnValid runs when the system registers

                    var primary = Get<UIContainerMissionResultsParameters>(rig.Results, "m_parameters");
                    Assert.That(primary.MissionDefinition, Is.SameAs(mission));
                    Assert.That(primary.LevelDefinition, Is.SameAs(level));
                    if (withSecondary)
                    {
                        var secondary = Get<UIContainerMissionResultsParameters>(rig.Secondary, "m_parameters");
                        Assert.That(secondary.MissionDefinition, Is.SameAs(mission));
                        Assert.That(secondary.LevelDefinition, Is.SameAs(level));
                    }
                    Assert.That(rig.Ranks.Show.Value, Is.False, "the ranks container was set up");
                });
            }
        }

        // With the level in a zone (0x67070C true) the previous accent is released (Unload at 0x67072B) and replaced by
        // UIContainerUtilities.GetZoneAccent (0x670765), whose image is loaded and published (0x670794); the act number is the
        // one-based position of the level's GUID in the zone's entries (the loop from 0x6707D0, leaving at the first match),
        // and the level image callback runs last (0x67084D), publishing no image for a level without one.
        [Test]
        public void ALevelInAZoneShowsTheZoneAccentTheActNumberAndNoBackground()
        {
            CharacterSelectScene.InitialiseUtilities();
            RunMissions(rig =>
            {
                StartMission(rig);
                GameplayLevelDefinition level = rig.Missions.CurrentLevel.LevelDefinition;
                WithoutLevelImage(level);
                GameplayLevelDefinition before = NewLevel(rig.Owned, "lucid-pause-before");
                GameplayLevelDefinition after = NewLevel(rig.Owned, "lucid-pause-after");
                var zone = rig.Owned.Track(ScriptableObject.CreateInstance<GameplayLevelCategory>());
                Set(zone, "m_guid", "lucid-pause-zone"); // authored GUID of a serialized definition
                Set(zone, "m_gameplayEntries", new List<GameplayLevelUIEntry> { before, level, after }); // Reflection: serialized m_gameplayEntries the editor assigns
                Sprite accent = NewSprite(rig.Owned);
                Cache(zone.ZoneAccent, accent);
                Get<List<GameplayLevelUIEntry>>(rig.Levels, "m_gameplayEntries").Add(zone);
                var previous = new ManagedAddressableAsset<Sprite>(null);
                Cache(previous, NewSprite(rig.Owned));
                previous.Load();
                previous.Load();
                Set(rig.Container, "m_levelManager", rig.Missions.Level); // Reflection: OnLevelManagerValid assigns it from the registry; the case skips that callback
                Set(rig.Container, "m_zoneAccent", previous); // Reflection: private zone accent that OnMissionManagerValid loads and later unloads
                Assert.That(Count(previous), Is.EqualTo(2));

                Call(rig.Container, "OnMissionManagerValid", rig.Missions.Manager); // Reflection: private callback that InvokeOnValid runs when the system registers

                Assert.That(Count(previous), Is.EqualTo(1), "released once");
                Assert.That(Get<ManagedAddressableAsset<Sprite>>(rig.Container, "m_zoneAccent"), Is.SameAs(zone.ZoneAccent));
                Assert.That(Count(zone.ZoneAccent), Is.EqualTo(1));
                Assert.That(rig.Container.ZoneIconAsset.Value, Is.SameAs(accent));
                Assert.That(rig.Container.DreamNumber.Value, Is.EqualTo(2));
                Assert.That(rig.Container.ActBackground.Value == null, Is.True);
            });
        }

        // A zone that lists other levels but not the current one: the loop runs out (0x6707FF, xor r12d, r12d) and zero is
        // published, overwriting the earlier value.
        [Test]
        public void AZoneThatDoesNotListTheLevelPublishesActNumberZero()
        {
            CharacterSelectScene.InitialiseUtilities();
            RunMissions(rig =>
            {
                StartMission(rig);
                GameplayLevelDefinition level = rig.Missions.CurrentLevel.LevelDefinition;
                WithoutLevelImage(level);
                var zone = rig.Owned.Track(ScriptableObject.CreateInstance<GameplayLevelCategory>());
                Set(zone, "m_guid", "lucid-pause-zone"); // authored GUID of a serialized definition
                Set(zone, "m_gameplayEntries", new List<GameplayLevelUIEntry> { level }); // Reflection: serialized m_gameplayEntries the editor assigns
                Cache(zone.ZoneAccent, NewSprite(rig.Owned));
                Get<List<GameplayLevelUIEntry>>(rig.Levels, "m_gameplayEntries").Add(zone);
                // The zone lookup finds the level through the cached level table; its entry list is then replaced, so the loop runs out.
                rig.Levels.CacheLevels();
                Set(zone, "m_gameplayEntries", new List<GameplayLevelUIEntry> // Reflection: serialized m_gameplayEntries the editor assigns
                {
                    NewLevel(rig.Owned, "lucid-pause-other-a"), NewLevel(rig.Owned, "lucid-pause-other-b"),
                });
                Set(rig.Container, "m_levelManager", rig.Missions.Level); // Reflection: OnLevelManagerValid assigns it from the registry; the case skips that callback
                rig.Container.DreamNumber.Value = 5;

                Call(rig.Container, "OnMissionManagerValid", rig.Missions.Manager); // Reflection: private callback that InvokeOnValid runs when the system registers

                Assert.That(rig.Container.DreamNumber.Value, Is.Zero);
            });
        }

        // The loop leaves at the first match (the je at 0x6707EF, with r12 = i + 1): a level listed twice takes its first position.
        [Test]
        public void ALevelListedTwiceInAZoneTakesTheFirstPosition()
        {
            CharacterSelectScene.InitialiseUtilities();
            RunMissions(rig =>
            {
                StartMission(rig);
                GameplayLevelDefinition level = rig.Missions.CurrentLevel.LevelDefinition;
                WithoutLevelImage(level);
                var zone = rig.Owned.Track(ScriptableObject.CreateInstance<GameplayLevelCategory>());
                Set(zone, "m_guid", "lucid-pause-zone"); // authored GUID of a serialized definition
                Set(zone, "m_gameplayEntries", new List<GameplayLevelUIEntry> // Reflection: serialized m_gameplayEntries the editor assigns
                {
                    NewLevel(rig.Owned, "lucid-pause-before"), level, level,
                });
                Cache(zone.ZoneAccent, NewSprite(rig.Owned));
                Get<List<GameplayLevelUIEntry>>(rig.Levels, "m_gameplayEntries").Add(zone);
                Set(rig.Container, "m_levelManager", rig.Missions.Level); // Reflection: OnLevelManagerValid assigns it from the registry; the case skips that callback

                Call(rig.Container, "OnMissionManagerValid", rig.Missions.Manager); // Reflection: private callback that InvokeOnValid runs when the system registers

                Assert.That(rig.Container.DreamNumber.Value, Is.EqualTo(2));
            });
        }

        // A serialized level with no loading image key: its image callback publishes no image.
        private static void WithoutLevelImage(GameplayLevelDefinition level) =>
            Set(level, "m_loadingImageReference", new AssetReferenceT<Sprite>("")); // Reflection: serialized reference, which a definition created in code lacks

        private static GameplayLevelDefinition NewLevel(Owned owned, string guid)
        {
            var level = Create<GameplayLevelDefinition>(owned);
            Set(level, "m_guid", guid); // authored GUID of a serialized definition
            Set(level, "m_sceneName", guid); // authored scene name
            return level;
        }

        // ---- Close, FireMissionQuitAnalytics, OnDestroy ----------------------------------------------------------------

        // Close 0x670D20 adds the resume event to the App's list, once.
        [Test]
        public void CloseQueuesTheResumeEventOnce()
        {
            RunMissions(rig =>
            {
                Call(rig.Container, "Close");
                Call(rig.Container, "Close");

                Assert.That(Events(rig), Is.EqualTo(new[] { rig.ResumeEvent }));
            });
        }

        // FireMissionQuitAnalytics 0x670C10 does nothing without an active mission state (0x670C58). With one it sends the quit
        // analytics through the active context (interface slot 22, 0x670CCF), whose regular implementation asks for the
        // CharacterManager first: that row is empty here, so the fault names it.
        [Test]
        public void TheQuitAnalyticsGoThroughTheActiveContext()
        {
            RunMissions(rig =>
            {
                Set(rig.Container, "m_missionManager", rig.Missions.Manager); // Reflection: OnMissionManagerValid assigns it from the registry; the case skips that callback
                Assert.DoesNotThrow(() => Call(rig.Container, "FireMissionQuitAnalytics"));

                StartMission(rig);

                var fault = Assert.Throws<NullReferenceException>(() => Call(rig.Container, "FireMissionQuitAnalytics"));
                StringAssert.Contains("CharacterManager", fault.Message);
            });
        }

        // OnDestroy 0x670DA0 runs the gameplay control base (which puts the remembered control state back), releases the zone
        // accent (Unload at 0x670E4C) and, when the App holds an OnPauseCloseEvent action, hands it to WaitForUI (0x670FF7)
        // and removes it (0x671096). The refusing coroutine host rejects the wait with the engine error expected here.
        [Test]
        public void DestroyingReleasesTheAccentAndForwardsThePauseCloseAction()
        {
            RunMissions(rig =>
            {
                var accent = new ManagedAddressableAsset<Sprite>(null);
                Cache(accent, NewSprite(rig.Owned));
                accent.Load();
                Set(rig.Container, "m_zoneAccent", accent); // Reflection: private zone accent that OnMissionManagerValid loads and later unloads
                Set(rig.Container, "m_levelManager", rig.Missions.Level); // Reflection: OnLevelManagerValid assigns it from the registry; the case skips that callback
                rig.App.Storage.SetValue<Action>(AppFSMKeys.OnPauseCloseEvent, () => { });
                rig.App.SetHasGameplayControl(true, true);

                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                Call(rig.Container, "OnDestroy");

                Assert.That(Count(accent), Is.Zero, "released");
                Assert.That(rig.App.Storage.HasValue<Action>(AppFSMKeys.OnPauseCloseEvent), Is.False, "removed");
                Assert.That(rig.App.HasGameplayControl(), Is.False, "the remembered state (off) is put back");
            });
        }

        // With no action stored nothing is forwarded (the HasValue test at 0x670F2F).
        [Test]
        public void DestroyingWithoutAPauseCloseActionStartsNoWait()
        {
            RunMissions(rig =>
            {
                Set(rig.Container, "m_levelManager", rig.Missions.Level); // Reflection: OnLevelManagerValid assigns it from the registry; the case skips that callback

                Assert.DoesNotThrow(() => Call(rig.Container, "OnDestroy"));

                LogAssert.NoUnexpectedReceived();
            });
        }

        // A published background image (ActBackground.Value tested with op_Inequality at 0x670E8A) makes the current level
        // release its level image (TryGetCurrentLevel 0x670EA0, UnloadLevelImage 0x670EB3); without one the image stays loaded.
        [Test]
        public void DestroyingReleasesTheLevelImageOnlyWhenABackgroundIsPublished()
        {
            foreach (bool published in new[] { true, false })
            {
                RunMissions(rig =>
                {
                    var levelImage = new ManagedAddressableAsset<Sprite>(null);
                    Cache(levelImage, NewSprite(rig.Owned));
                    levelImage.Load();
                    // Reflection: stands for the wrapper an earlier LoadLevelImage request made.
                    Set(rig.Missions.CurrentLevel.LevelDefinition, "m_managedLoadingImage", levelImage);
                    Set(rig.Container, "m_levelManager", rig.Missions.Level);
                    if (published)
                        rig.Container.ActBackground.Value = NewSprite(rig.Owned);

                    Call(rig.Container, "OnDestroy");

                    Assert.That(Count(levelImage), Is.EqualTo(published ? 0 : 1), "published " + published);
                });
            }
        }

        // ---- The actions ----------------------------------------------------------------------------------------------

        // Action_Restart 0x670870 and Action_Respawn 0x670980 each hand one new iterator to CoroutineUtils.RunCoroutine; the
        // refusing host logs one engine error for each and returns no coroutine, which is then what is stored (+0xA8).
        [Test]
        public void RestartAndRespawnEachStartOneCoroutine()
        {
            RunMissions(rig =>
            {
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                rig.Container.Action_Restart();
                LogAssert.NoUnexpectedReceived();
                Assert.That(Get<object>(rig.Container, "m_closingCoroutine"), Is.Null);

                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                rig.Container.Action_Respawn();
                LogAssert.NoUnexpectedReceived();
                Assert.That(Get<object>(rig.Container, "m_closingCoroutine"), Is.Null);
            });
        }

        // ---- The Respawn and Restart iterators ------------------------------------------------------------------------

        private sealed class CharacterRig
        {
            internal Owned Owned;
            internal CharacterManager Characters;
            internal Character Character;
            internal App App;
            internal ScriptedExclusivityContainerManager Containers;
            internal UIContainerPause Container;
            internal UIContainerIdentifier Identifier;
            internal UIModernEvent ResumeEvent;
        }

        private static void RunCharacters(Action<CharacterRig> body)
        {
            SettingsPageTestSupport.InitialiseAnalytics(); // the collector's type initialiser runs over owned rows here, not in whichever case reports first
            WithManager((owned, characters, character) =>
            {
                owned.LeaseRow<AudioManager>(); // LevelManager's field initialiser
                var rig = new CharacterRig { Owned = owned, Characters = characters, Character = character, App = ProcessManager.GetSystem<App>() };
                rig.App.Storage.SetValue(AppFSMKeys.UIModernEvents, new List<UIModernEvent>());
                UIManager uiManager = NewUiManager(owned, out rig.Containers);
                rig.Identifier = NewIdentifier(owned, "pause");
                rig.Container = owned.Host("Lucid pause", false).AddComponent<UIContainerPause>();
                rig.ResumeEvent = NewEvent(owned, "pause-resume");
                // Serialized in the original, and the callback Start's InvokeOnValid runs.
                Set(rig.Container, "m_resumeEvent", rig.ResumeEvent);
                Set(rig.Container, "m_identifier", rig.Identifier); // Reflection: serialized m_identifier the editor assigns
                Set(rig.Container, "m_uiManager", uiManager); // Reflection: Start's InvokeOnValid callback assigns it; the case skips Start
                body(rig);
            });
        }

        private static bool Waiting(object current) => ((WaitUntil)current).keepWaiting;

        // The Respawn iterator 0x671420 queues the resume event (0x671595) and, while the pause menu is not open (IsOpen at
        // 0x6715B0 false), yields a WaitUntil on the same test (b__0 at 0x6713C0: the UIManager's IsOpen for the container's
        // identifier). It ends when that wait does.
        [Test]
        public void RespawnQueuesTheResumeEventThenWaitsForTheMenuToBeOpen()
        {
            RunCharacters(rig =>
            {
                IEnumerator respawn = (IEnumerator)Call(rig.Container, "Respawn");

                Assert.That(respawn.MoveNext(), Is.True);

                Assert.That(rig.App.GetUIModernEvents(), Is.EqualTo(new[] { rig.ResumeEvent }));
                Assert.That(respawn.Current, Is.InstanceOf<WaitUntil>());
                Assert.That(Waiting(respawn.Current), Is.True, "the predicate is the manager's IsOpen, still false");
                rig.Containers.Open.Add(rig.Identifier);
                Assert.That(Waiting(respawn.Current), Is.False);
                Assert.That(respawn.MoveNext(), Is.False, "no character is current");
            });
        }

        // An already open menu skips the wait (the jne at 0x6715B7), and a current character is put out of bounds (the
        // SetValue<bool> of IsOutOfBounds with true, 0x671828) in the same step.
        [Test]
        public void RespawnOfAnOpenMenuPutsTheCurrentCharacterOutOfBoundsAtOnce()
        {
            RunCharacters(rig =>
            {
                rig.Containers.Open.Add(rig.Identifier);
                SetCurrent(rig.Characters, rig.Character, true);
                IEnumerator respawn = (IEnumerator)Call(rig.Container, "Respawn");
                Assert.That(rig.Character.Storage.TryGetValue(ActorFSMKeys.IsOutOfBounds, out bool unsetBefore), Is.False, "unset before");

                Assert.That(respawn.MoveNext(), Is.False);

                Assert.That(rig.Character.Storage.TryGetValue(ActorFSMKeys.IsOutOfBounds, out bool outOfBounds), Is.True);
                Assert.That(outOfBounds, Is.True);
                Assert.That(rig.App.GetUIModernEvents(), Is.EqualTo(new[] { rig.ResumeEvent }));
            });
        }

        // The Restart iterator 0x6718E0 queues the resume event (0x671AA0) and yields a WaitUntil on the same IsOpen test
        // (b__23_0, 0x671300) without a pre-check; the next step restarts the level (LevelManager.Restart, 0x6719AC) and the
        // iterator ends. Restart queues the level's restart event and marks the restart in progress.
        [Test]
        public void RestartQueuesTheResumeEventWaitsForTheMenuThenRestartsTheLevel()
        {
            RunCharacters(rig =>
            {
                StartRestartableLevel(rig, out UIModernEvent restartEvent);
                IEnumerator restart = (IEnumerator)Call(rig.Container, "Restart");
                try
                {
                    Assert.That(restart.MoveNext(), Is.True);

                    Assert.That(rig.App.GetUIModernEvents(), Is.EqualTo(new[] { rig.ResumeEvent }));
                    Assert.That(restart.Current, Is.InstanceOf<WaitUntil>());
                    Assert.That(Waiting(restart.Current), Is.True, "the predicate is the manager's IsOpen, still false");
                    rig.Containers.Open.Add(rig.Identifier);
                    Assert.That(Waiting(restart.Current), Is.False);
                    Assert.That(restart.MoveNext(), Is.False);

                    Assert.That(rig.App.GetUIModernEvents(), Is.EqualTo(new[] { rig.ResumeEvent, restartEvent }));
                    Assert.That(rig.App.Storage.TryGetValue(AppFSMKeys.RestartInProgress, out bool inProgress), Is.True);
                    Assert.That(inProgress, Is.True);
                }
                finally
                {
                    rig.App.Storage.RemoveValue<bool>(AppFSMKeys.RestartInProgress);
                    rig.App.Storage.RemoveValue<bool>(AppFSMKeys.IsInPostGame);
                }
            });
        }

        // Unlike Respawn there is no IsOpen check before the wait (0x671AA0 runs on into 0x671AE8): a menu that is already open
        // still yields a WaitUntil, which is already satisfied, and only the next step ends the iterator.
        [Test]
        public void RestartOfAnOpenMenuStillYieldsAWaitThatIsAlreadySatisfied()
        {
            RunCharacters(rig =>
            {
                rig.Containers.Open.Add(rig.Identifier);
                rig.Owned.Register(rig.Owned.Host("Lucid pause level manager", false).AddComponent<LevelManager>());
                IEnumerator restart = (IEnumerator)Call(rig.Container, "Restart");

                Assert.That(restart.MoveNext(), Is.True);

                Assert.That(restart.Current, Is.InstanceOf<WaitUntil>());
                Assert.That(Waiting(restart.Current), Is.False);
                Assert.That(restart.MoveNext(), Is.False);
            });
        }

        // A level manager with a current level, the App and a restart event: the state LevelManager.Restart (0x06002460) needs.
        private static void StartRestartableLevel(CharacterRig rig, out UIModernEvent restartEvent)
        {
            var levelManager = rig.Owned.Register(rig.Owned.Host("Lucid pause level manager", false).AddComponent<LevelManager>());
            restartEvent = NewEvent(rig.Owned, "pause-level-restart");
            GameplayLevelDefinition definition = NewLevel(rig.Owned, "lucid-pause-restart-level");
            // Reflection: LevelManager.Initialise builds these from a loaded level and the serialized event.
            Set(levelManager, "m_currentLevel", new LevelManagerLevel(definition, definition));
            Set(levelManager, "m_app", rig.App);
            Set(levelManager, "m_restartUIEvent", restartEvent); // Reflection: serialized m_restartUIEvent the editor assigns
        }
    }
}
