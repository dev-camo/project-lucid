using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The gameplay HUD containers UIContainerGameplayIsland (x86-64 0x635B00..0x635FE0), UIContainerGameplayControl
    // (0x6358E0), UIContainerLeaderboards (0x6363D0..0x636690), UIContainerLevelDefinition (0x636760..0x6369C0),
    // UIContainerOrbCollectableCounter (0x66C870..0x66D360 and its iterator 0x66D360), UIContainerRewardRestriction
    // (0x678C90), UIContainerNotificationRequest (0x66BD10) and UIContainerSkipCutscene (0x67EFD0..0x67F170).
    // Each is a real component on an owned inactive host, so its Unity messages are called directly. Paths that need
    // Addressables, the control mapping or the configuration assets are not covered: UIContainerLeaderboards.Start and
    // OnDestroy, UIContainerLevelDefinition.Setup's image load, UIContainerRewardRestriction.Setup,
    // the UIContainerOrbCollectableCounter icon load that ends UpdateBindings and the UIContainerUtilities bodies.
    public sealed class UIContainerHudBindingTests
    {
        private static T Add<T>(Owned owned, string name) where T : Component => owned.Host(name, false).AddComponent<T>();

        // ---- UIContainerGameplayIsland -------------------------------------------------------------------------

        private static GameplayIslandDefinition Island(Owned owned, Strings name)
        {
            var definition = owned.Track(ScriptableObject.CreateInstance<GameplayIslandDefinition>());
            Set(definition, "m_localisedId", name); // serialized authored data
            return definition;
        }

        // OnGameplayIslandChanged 0x635CF0: the name is the first active island's key (IReadOnlyList slot 0 at 0x635e00),
        // or Strings.NONE (0xb6fa1284) for an empty list, and the name counts as valid when it is not NONE (0x635e32).
        [Test]
        public void TheIslandNameIsTheFirstActiveIslandsKey()
        {
            Owned.Run(owned =>
            {
                var island = Add<UIContainerGameplayIsland>(owned, "Lucid gameplay island");
                var first = (Strings)0x1234;
                var second = (Strings)0x5678;
                Call(island, "OnGameplayIslandChanged", new List<GameplayIslandDefinition>());
                Assert.That(island.IslandName.Value, Is.EqualTo(Strings.NONE));
                Assert.That(island.IslandNameValid.Value, Is.False);

                Call(island, "OnGameplayIslandChanged", new List<GameplayIslandDefinition> { Island(owned, first), Island(owned, second) });
                Assert.That(island.IslandName.Value, Is.EqualTo(first), "the first island, not the last");
                Assert.That(island.IslandNameValid.Value, Is.True);

                Call(island, "OnGameplayIslandChanged", new List<GameplayIslandDefinition> { Island(owned, Strings.NONE) });
                Assert.That(island.IslandName.Value, Is.EqualTo(Strings.NONE));
                Assert.That(island.IslandNameValid.Value, Is.False, "an island with no name is not a valid name");
            });
        }

        // Awake 0x635B00 hands the manager a callback (<Awake>b__5_0) that subscribes OnGameplayIslandChanged and re-enters
        // the active island (0x636049, 0x63605d), which reports at once; OnDestroy 0x635BE0 removes the subscription.
        [Test]
        public void AwakeSubscribesToTheIslandManagerAndOnDestroyUnsubscribes()
        {
            Owned.Run(owned =>
            {
                owned.LeaseActions(SystemAction.Initialise, SystemAction.Shutdown);
                owned.LeaseRow<LevelManager>();
                owned.LeaseRow<CharacterManager>();
                GameplayIslandManager manager = owned.Register(new GameplayIslandManager());
                var island = Add<UIContainerGameplayIsland>(owned, "Lucid gameplay island");
                island.IslandName.Value = (Strings)0x4321;
                island.IslandNameValid.Value = true;

                Call(island, "Awake");
                Assert.That(Subscribers(manager), Is.EqualTo(new object[] { island }));
                Assert.That(island.IslandName.Value, Is.EqualTo(Strings.NONE), "the re-entry reported an empty island list");
                Assert.That(island.IslandNameValid.Value, Is.False);

                Call(island, "OnDestroy");
                Assert.That(Subscribers(manager), Is.Empty);
            });
        }

        private static object[] Subscribers(GameplayIslandManager manager)
        {
            var handlers = Get<Action<IReadOnlyList<GameplayIslandDefinition>>>(manager, "OnIslandDefinitionChanged");
            if (handlers == null) return new object[0];
            var targets = new List<object>();
            foreach (Delegate handler in handlers.GetInvocationList()) targets.Add(handler.Target);
            return targets.ToArray();
        }

        // OnDestroy 0x635BE0 tests the reference first (IsValid 0x635c83), so a container destroyed without a manager is quiet.
        [Test]
        public void OnDestroyWithoutAnIslandManagerDoesNothing()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<GameplayIslandManager>();
                var island = Add<UIContainerGameplayIsland>(owned, "Lucid gameplay island");
                Assert.DoesNotThrow(() => Call(island, "OnDestroy"));
            });
        }

        // ---- UIContainerLeaderboards ---------------------------------------------------------------------------

        // ShowLeaderboards 0x6365C0 and the controller entry 0x636550 return for TailsChallenge_HourlyQA (0x9b4bc38d) and
        // TailsChallenges_Weekly (0xa45d8a7f) before the manager is read; any other identifier reads it (SystemRef.Get
        // 0x6365fd), which faults here because no TimeTrialLeaderboardManager is registered. Action_IssueChallenge
        // (0x636640) has no such filter.
        [Test]
        public void TheTailsChallengeBoardsAreNeverShownFromTheContainer()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<TimeTrialLeaderboardManager>();
                var leaderboards = Add<UIContainerLeaderboards>(owned, "Lucid leaderboards");
                foreach (LeaderboardIdentifier blocked in new[] { LeaderboardIdentifier.TailsChallenge_HourlyQA, LeaderboardIdentifier.TailsChallenges_Weekly })
                {
                    leaderboards.SetLeaderboard(blocked);
                    Assert.DoesNotThrow(() => leaderboards.ShowLeaderboards(), blocked.ToString());
                    Assert.DoesNotThrow(() => Call(leaderboards, "ControllerShowLeaderboards", 0f), blocked + " from the controller");
                    Assert.Throws<NullReferenceException>(() => leaderboards.Action_IssueChallenge(), blocked + " is still challenged");
                }
                foreach (LeaderboardIdentifier shown in new[] { LeaderboardIdentifier.None, LeaderboardIdentifier.TailsChallenges_Daily, LeaderboardIdentifier.TimeTrials_B1AXMX })
                {
                    leaderboards.SetLeaderboard(shown);
                    Assert.Throws<NullReferenceException>(() => leaderboards.ShowLeaderboards(), shown.ToString());
                    Assert.Throws<NullReferenceException>(() => Call(leaderboards, "ControllerShowLeaderboards", 0f), shown + " from the controller");
                }
            });
        }

        // SetLeaderboard 0x636630 stores the identifier in the serialized field.
        [Test]
        public void SetLeaderboardStoresTheIdentifier()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<TimeTrialLeaderboardManager>();
                var leaderboards = Add<UIContainerLeaderboards>(owned, "Lucid leaderboards");
                leaderboards.SetLeaderboard(LeaderboardIdentifier.TimeTrials_B1AXMX);
                Assert.That(Get<LeaderboardIdentifier>(leaderboards, "m_leaderboardIdentifier"), Is.EqualTo(LeaderboardIdentifier.TimeTrials_B1AXMX));
            });
        }

        // ---- UIContainerLevelDefinition ------------------------------------------------------------------------

        private static Sprite NewSprite(Owned owned)
        {
            var texture = owned.Track(new Texture2D(2, 2));
            return owned.Track(Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.zero));
        }

        // The image callback <Setup>b__4_0 (0x6369C0) stores the sprite and publishes it; SetMissionDefinition 0x636860
        // publishes the level sprite when the mission has no loading image override (TryGetLoadingImageOverride 0x636884),
        // and remembers the mission for OnDestroy.
        [Test]
        public void TheLoadingImageIsTheLevelImageUnlessTheMissionOverridesIt()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<TimeTrialLeaderboardManager>(); // MissionDefinition's field initialiser
                var container = Add<UIContainerLevelDefinition>(owned, "Lucid level definition");
                var mission = owned.Track(ScriptableObject.CreateInstance<MissionDefinition>());
                Set(mission, "m_overrideLoadingImage", new AssetReferenceT<Sprite>("")); // serialized authored data: no override key
                Sprite levelImage = NewSprite(owned);

                container.SetMissionDefinition(mission);
                Assert.That(container.LoadingImage.Value == null, Is.True, "no level image has arrived yet");
                Assert.That(Get<MissionDefinition>(container, "m_missionDefinition"), Is.SameAs(mission));

                Call(container, "<Setup>b__4_0", levelImage);
                Assert.That(container.LoadingImage.Value, Is.SameAs(levelImage));
                Assert.That(Get<Sprite>(container, "m_levelSprite"), Is.SameAs(levelImage));

                container.LoadingImage.Value = null;
                container.SetMissionDefinition(mission);
                Assert.That(container.LoadingImage.Value, Is.SameAs(levelImage), "a mission without an override shows the level image");
            });
        }

        // Setup 0x636760 compares the new level with the held one by GUID (ScriptableObjectWithGuid.op_Equality 0x6367d0)
        // and returns before touching the held level's image or loading another.
        [Test]
        public void SetupWithTheSameLevelByGuidChangesNothing()
        {
            Owned.Run(owned =>
            {
                var container = Add<UIContainerLevelDefinition>(owned, "Lucid level definition");
                var held = owned.Track(ScriptableObject.CreateInstance<GameplayLevelDefinition>());
                Set(held, "m_guid", "lucid-hud-level"); // serialized authored data
                var sameGuid = owned.Track(ScriptableObject.CreateInstance<GameplayLevelDefinition>());
                Set(sameGuid, "m_guid", "lucid-hud-level");
                // Setup's own image load needs Addressables, so the level it would have stored is set directly.
                Set(container, "m_levelDefinition", held);

                container.Setup(new UIContainerLevelViewParameters(sameGuid));
                Assert.That(Get<GameplayLevelDefinition>(container, "m_levelDefinition"), Is.SameAs(held), "a level with the same GUID is not adopted");
            });
        }

        // OnDestroy 0x6368F0 releases only definitions it holds.
        [Test]
        public void OnDestroyWithNoDefinitionsDoesNothing()
        {
            Owned.Run(owned =>
            {
                var container = Add<UIContainerLevelDefinition>(owned, "Lucid level definition");
                Assert.DoesNotThrow(() => Call(container, "OnDestroy"));
            });
        }

        // ---- UIContainerOrbCollectableCounter ------------------------------------------------------------------

        private static UIContainerOrbCollectableCounter Counter(Owned owned)
        {
            owned.LeaseRow<DataManager>(); // the counter's field initialiser
            return Add<UIContainerOrbCollectableCounter>(owned, "Lucid orb counter");
        }

        // Awake 0x66C870 switches a counter with no reward type off; one with a reward type waits for the progression
        // manager instead (not driven here).
        [Test]
        public void ACounterWithoutARewardTypeDisablesItself()
        {
            Owned.Run(owned =>
            {
                UIContainerOrbCollectableCounter counter = Counter(owned);
                Assert.That(counter.enabled, Is.True);
                Call(counter, "Awake");
                Assert.That(counter.enabled, Is.False);
            });
        }

        private static float Seconds(object wait) => Get<float>(wait, "m_Seconds");

        // AnimateCount iterator 0x66D360: wait the initial delay, then while the shown count is below the target wait one
        // tick and add one (state 2 increments, 0x66d3fc..0x66d442). Counting from 3 to 5 takes the delay, then two ticks.
        [Test]
        public void TheCountTicksUpToTheTargetAfterTheInitialDelay()
        {
            Owned.Run(owned =>
            {
                UIContainerOrbCollectableCounter counter = Counter(owned);
                Set(counter, "m_initialDelaySeconds", 0.5f);
                Set(counter, "m_animateTickIntervalSeconds", 0.25f);
                counter.CollectedAmount.Value = 3;
                var steps = (IEnumerator)Call(counter, "AnimateCount", 5);

                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(Seconds(steps.Current), Is.EqualTo(0.5f), "the initial delay");
                Assert.That(counter.CollectedAmount.Value, Is.EqualTo(3));
                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(Seconds(steps.Current), Is.EqualTo(0.25f), "the first tick");
                Assert.That(counter.CollectedAmount.Value, Is.EqualTo(3), "the count rises after the tick, not before");
                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(Seconds(steps.Current), Is.EqualTo(0.25f), "the second tick");
                Assert.That(counter.CollectedAmount.Value, Is.EqualTo(4));
                Assert.That(steps.MoveNext(), Is.False);
                Assert.That(counter.CollectedAmount.Value, Is.EqualTo(5));
            });
        }

        // A count already at the target waits only the initial delay.
        [Test]
        public void ACountAtTheTargetWaitsOnlyTheInitialDelay()
        {
            Owned.Run(owned =>
            {
                UIContainerOrbCollectableCounter counter = Counter(owned);
                Set(counter, "m_initialDelaySeconds", 0.5f);
                counter.CollectedAmount.Value = 5;
                var steps = (IEnumerator)Call(counter, "AnimateCount", 5);
                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(steps.MoveNext(), Is.False);
                Assert.That(counter.CollectedAmount.Value, Is.EqualTo(5));
            });
        }

        // UpdateValues(Func) 0x66CFC0 takes the reward type from the function, and releases the previous type's icon
        // through the DataManager row only when there was one and it changed (cmp 0x66d00b, 0x66d010). Here no
        // DataManager is registered, so releasing faults; an unchanged or first type does not reach it. The mission
        // overload (0x66D100) reads MissionDefinition.RewardType, which is Orb unless authored otherwise.
        [Test]
        public void ChangingTheRewardTypeReleasesThePreviousIcon()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<TimeTrialLeaderboardManager>(); // MissionDefinition's field initialiser
                UIContainerOrbCollectableCounter counter = Counter(owned);
                var mission = owned.Track(ScriptableObject.CreateInstance<MissionDefinition>());

                counter.UpdateValues(mission);
                Assert.That(Get<CollectableType>(counter, "m_rewardType"), Is.EqualTo(CollectableType.Orb), "the first type: nothing to release");
                counter.UpdateValues(mission);
                Assert.That(Get<CollectableType>(counter, "m_rewardType"), Is.EqualTo(CollectableType.Orb), "the same type: nothing to release");

                Set(counter, "m_rewardType", CollectableType.Moon); // serialized authored data
                Assert.Throws<NullReferenceException>(() => counter.UpdateValues(mission), "the Moon icon is released through the DataManager row");
                Assert.That(Get<CollectableType>(counter, "m_rewardType"), Is.EqualTo(CollectableType.Orb), "the new type is stored before the release");
            });
        }

        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");

        // UpdateBindings 0x66CB30 over the real ProgressionManager, MissionManager and a regular mission context. HasValues is
        // Collected > 0 (test/setg 0x66cba2). The count is shown ahead of the saved total only when the counter animates, the
        // mission is not a replay (0x66cbb9) and rewards are pending (test/jle 0x66cc36); the count is then collected - pending
        // (0x66cc46) and a coroutine ticks it up. Everything is written before the icon load (0x66cd29), which faults here
        // because no DataManager is registered. Rows: animate, replay, completed (2 pending orbs), collected, shown, has values.
        [Test]
        public void TheShownCountIsAheadOfTheSavedTotalOnlyForAnAnimatedPendingReward()
        {
            var rows = new[]
            {
                (animate: true, replay: false, completed: true, collected: 5, shown: 3, hasValues: true),
                (animate: true, replay: true, completed: true, collected: 5, shown: 5, hasValues: true),
                (animate: false, replay: false, completed: true, collected: 5, shown: 5, hasValues: true),
                (animate: true, replay: false, completed: true, collected: 0, shown: -2, hasValues: false),
                (animate: true, replay: false, completed: false, collected: 5, shown: 5, hasValues: true),
            };
            foreach (var row in rows)
            {
                string label = row.ToString();
                OriginalPersistentLevelObjectTests.WithMissions(true, (owned, missions) =>
                {
                    owned.LeaseRow<TimeTrialLeaderboardManager>(); // MissionDefinition's field initialiser
                    owned.LeaseRow<UIManager>(); // MissionState's field initialiser
                    owned.LeaseRow<ChallengeManager>(); // MissionContextRegular's field initialiser
                    var mission = owned.Track(ScriptableObject.CreateInstance<MissionDefinition>());
                    Set(mission, "m_guid", "lucid-orb-counter-mission"); // serialized authored data
                    Set(mission, "m_subMissions", new List<MissionDefinition>());
                    Set(mission, "m_rewardValue", 2);
                    missions.Manager.SetActiveMissionContext(new MissionContextRegular(mission, missions.CurrentLevel.LevelDefinition, row.replay));
                    if (row.completed)
                    {
                        Assert.That(missions.Manager.TryGetActiveMissionState(out MissionState state), Is.True, label);
                        LogAssert.Expect(LogType.Error, RefusedCoroutine); // the completion's save request
                        Call(state, "MarkComplete");
                        Assert.That(missions.Manager.GetPendingRewards(CollectableType.Orb), Is.EqualTo(2), label);
                    }

                    // The totals are authored into a real cached save: Initialise would cache the whole zone graph on open.
                    var handler = new ProgressionCollectableHandler();
                    handler.CacheEmptyProgress(missions.Save.CurrentSaveIndex);
                    if (row.collected > 0)
                        ((IDictionary)Get<object>(handler.ProgressPerSave[missions.Save.CurrentSaveIndex], "m_total"))[CollectableType.Orb] =
                            new ProgressionCollectable(CollectableType.Orb, row.collected, 10);
                    var progression = new ProgressionManager();
                    Set(progression, "m_saveManager", missions.Save); // set by Initialise, which also caches every zone
                    Set(progression, "m_collectables", handler);

                    UIContainerOrbCollectableCounter counter = Counter(owned);
                    Set(counter, "m_rewardType", CollectableType.Orb); // serialized authored data
                    Set(counter, "m_animatePendingCount", row.animate); // serialized authored data
                    if (row.animate && !row.replay && row.completed) LogAssert.Expect(LogType.Error, RefusedCoroutine); // the inactive host
                    Assert.Throws<NullReferenceException>(() => Call(counter, "ProgressionManagerValid", progression), label);
                    Assert.That(counter.CollectedAmount.Value, Is.EqualTo(row.shown), label);
                    Assert.That(counter.HasValues.Value, Is.EqualTo(row.hasValues), label);
                });
            }
        }

        // OnDestroy 0x66CDF0 guards the icon release with SystemRef.IsNull (0x66ced3) and the reward type.
        [Test]
        public void OnDestroyReleasesNoIconWithoutADataManager()
        {
            Owned.Run(owned =>
            {
                UIContainerOrbCollectableCounter counter = Counter(owned);
                Set(counter, "m_rewardType", CollectableType.Orb); // serialized authored data
                Assert.DoesNotThrow(() => Call(counter, "OnDestroy"));
            });
        }

        // ---- UIContainerRewardRestriction ----------------------------------------------------------------------

        // OnDisable 0x678C90 releases the held definition's icon, once, and nothing when none is held.
        [Test]
        public void DisablingTheRestrictionReleasesTheHeldIconOnce()
        {
            Owned.Run(owned =>
            {
                var restriction = Add<UIContainerRewardRestriction>(owned, "Lucid reward restriction");
                Assert.DoesNotThrow(() => Call(restriction, "OnDisable"), "no definition held");

                var definition = owned.Track(ScriptableObject.CreateInstance<CollectableDefinition>());
                Set(definition, "m_refCount", 2); // the count LoadIcon would have left after two loads
                Set(restriction, "m_collectableDefinition", definition);
                Call(restriction, "OnDisable");
                Assert.That(Get<int>(definition, "m_refCount"), Is.EqualTo(1), "UnloadIcon ran once");
            });
        }

        // ---- UIContainerNotificationRequest --------------------------------------------------------------------

        private static object NewConfig(NotificationType notification, Sprite sprite, Strings text)
        {
            object config = Activator.CreateInstance(typeof(UIContainerNotificationRequest).GetNestedType("NotificationPermissionConfig", System.Reflection.BindingFlags.NonPublic));
            Set(config, "Notification", notification);
            Set(config, "Sprite", sprite);
            Set(config, "String", text);
            return config;
        }

        private static UIContainerNotificationRequest Request(Owned owned, Image image, LocalisedUIString label, params object[] configs)
        {
            var request = Add<UIContainerNotificationRequest>(owned, "Lucid notification request");
            Array types = Array.CreateInstance(typeof(UIContainerNotificationRequest).GetNestedType("NotificationPermissionConfig", System.Reflection.BindingFlags.NonPublic), configs.Length);
            for (int index = 0; index < configs.Length; index++) types.SetValue(configs[index], index);
            Set(request, "m_notificationTypes", types);
            Set(request, "m_displayImage", image);
            Set(request, "m_displayString", label);
            return request;
        }

        // Setup 0x66BD10 takes the NotificationManager (0x66bd81) and the requested type, then shows the sprite and string
        // of every configuration of that type (0x66bdd5..0x66bdf5). A configuration of another type is left alone.
        [Test]
        public void SetupShowsOnlyTheRequestedNotificationType()
        {
            Owned.Run(owned =>
            {
                NotificationManager manager = owned.Register(Add<NotificationManager>(owned, "Lucid notification manager"));
                var image = Add<Image>(owned, "Lucid notification image");
                var label = Add<LocalisedUIString>(owned, "Lucid notification label");
                Sprite sprite = NewSprite(owned);
                UIContainerNotificationRequest request = Request(owned, image, label, NewConfig(NotificationType.ChallengeRefresh, sprite, (Strings)0x777));

                request.Setup(new UIContainerNotificationRequestParameters(NotificationType.LapsedPlayer));
                Assert.That(Get<NotificationManager>(request, "m_notificationManager"), Is.SameAs(manager));
                Assert.That(Get<NotificationType>(request, "m_notification"), Is.EqualTo(NotificationType.LapsedPlayer));
                Assert.That(image.sprite == null, Is.True, "no configuration of the requested type");
                Assert.That(label.Identifier, Is.EqualTo(Strings.NONE));
            });
        }

        // A matching configuration is shown (sprite 0x66bde5, string 0x66bdf5) before the request is recorded with the
        // manager (IsRequestingNotification 0x66be05); the unstarted manager has no save manager and faults there.
        [Test]
        public void SetupShowsAMatchingConfigurationBeforeRecordingTheRequest()
        {
            Owned.Run(owned =>
            {
                owned.Register(Add<NotificationManager>(owned, "Lucid notification manager"));
                var image = Add<Image>(owned, "Lucid notification image");
                var label = Add<LocalisedUIString>(owned, "Lucid notification label");
                Sprite sprite = NewSprite(owned);
                UIContainerNotificationRequest request = Request(owned, image, label, NewConfig(NotificationType.ChallengeRefresh, sprite, (Strings)0x777));

                Assert.Throws<NullReferenceException>(() => request.Setup(new UIContainerNotificationRequestParameters(NotificationType.ChallengeRefresh)));
                Assert.That(image.sprite, Is.SameAs(sprite));
                Assert.That(label.Identifier, Is.EqualTo((Strings)0x777));
            });
        }

        // The manager is taken before the parameters are read (0x66bd81 then 0x66bda3), and a parameter object of another
        // type reads as null and faults at 0x66bda7.
        [Test]
        public void SetupTakesTheManagerBeforeItReadsTheParameters()
        {
            Owned.Run(owned =>
            {
                NotificationManager manager = owned.Register(Add<NotificationManager>(owned, "Lucid notification manager"));
                UIContainerNotificationRequest request = Request(owned, null, null);
                Assert.Throws<NullReferenceException>(() => request.Setup(null));
                Assert.That(Get<NotificationManager>(request, "m_notificationManager"), Is.SameAs(manager));
                Assert.Throws<NullReferenceException>(() => request.Setup(new UIContainerLevelViewParameters(null)));
            });
        }

        // ---- UIContainerGameplayControl ------------------------------------------------------------------------

        // Start 0x6358E0 remembers whether gameplay control was active (GetValue<bool> of GameplayControlActive) and then
        // applies the serialized choice through App.SetHasGameplayControl; OnDestroy 0x635A30 puts the remembered value back.
        [Test]
        public void GameplayControlIsAppliedOnStartAndRestoredOnDestroy()
        {
            Owned.Run(owned =>
            {
                owned.Lease(new OwnedCharacterManagers());
                App app = ProcessManager.GetSystem<App>();
                var events = new List<bool>();
                Action<bool> record = events.Add;
                app.OnGameplayControlActive += record;
                owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() => app.OnGameplayControlActive -= record));
                var control = Add<UIContainerGameplayControl>(owned, "Lucid gameplay control");

                app.SetHasGameplayControl(true, true);
                events.Clear();
                Set(control, "m_enableGameplayControl", false); // serialized authored data
                Call(control, "Start");
                Assert.That(Get<bool>(control, "m_previousGameplayControl"), Is.True);
                Assert.That(app.HasGameplayControl(), Is.False);
                Call(control, "OnDestroy");
                Assert.That(app.HasGameplayControl(), Is.True, "the remembered control is restored");
                Assert.That(events, Is.EqualTo(new[] { false, true }));

                app.SetHasGameplayControl(false, true);
                events.Clear();
                Set(control, "m_enableGameplayControl", true);
                Call(control, "Start");
                Assert.That(Get<bool>(control, "m_previousGameplayControl"), Is.False);
                Assert.That(app.HasGameplayControl(), Is.True);
                Call(control, "OnDestroy");
                Assert.That(app.HasGameplayControl(), Is.False);
                Assert.That(events, Is.EqualTo(new[] { true, false }));
            });
        }

        // ---- UIContainerSkipCutscene ---------------------------------------------------------------------------

        private static void WithCutscenes(Action<Owned, App, CutsceneManager> body) => Owned.Run(owned =>
        {
            // Taken first, so it is released last: it owns the App, DataManager and SaveManager rows.
            owned.Lease(new OwnedCharacterManagers());
            App app = ProcessManager.GetSystem<App>();
            // The CutsceneManager constructor registers itself and subscribes both actions; its field initialisers
            // create these three rows. It claims its own absent row first, as the constructor publishes into it.
            owned.LeaseActions(SystemAction.Initialise, SystemAction.Shutdown);
            owned.LeaseRow<FullscreenShaderManager>();
            owned.LeaseRow<VisualQualityManager_SDT>();
            owned.LeaseRow<CinemachineCameraManager>();
            var row = owned.Lease(new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<CutsceneManager>()));
            row.Register(null);
            var manager = new CutsceneManager();
            owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() => ProcessManager.UnsubscribeFromAllActions(manager)));
            row.AdoptCurrent();
            owned.LeaseRow<UIManager>(); // the container's field initialiser
            body(owned, app, manager);
        });

        // Action_OnSkipOnce 0x67F170 flags a tap and Action_OnFullSkip 0x67F100 flags a hold (the setters at 0x79faa4 and
        // 0x79fab4 write different fields); with no UIManager registered the container is not closed.
        [Test]
        public void TheSkipActionsFlagTheCutsceneManager()
        {
            WithCutscenes((owned, app, cutscenes) =>
            {
                var skip = Add<UIContainerSkipCutscene>(owned, "Lucid skip cutscene");
                Call(skip, "Start");

                skip.Action_OnSkipOnce();
                Assert.That(cutscenes.ConsumeSkipTapEvent(), Is.True);
                Assert.That(cutscenes.ConsumeSkipHoldEvent(), Is.False, "a tap is not a hold");

                skip.Action_OnFullSkip();
                Assert.That(cutscenes.ConsumeSkipHoldEvent(), Is.True);
                Assert.That(cutscenes.ConsumeSkipTapEvent(), Is.False, "a hold is not a tap");
            });
        }

        // Start 0x67EFD0 takes the App and CutsceneManager, and sets the hold-to-skip label to the theatre string only while
        // a theatre session is stored (HasValue<CutsceneTheatre> 0x67f0d2).
        [Test]
        public void StartLabelsTheSkipInputOnlyDuringATheatreSession()
        {
            WithCutscenes((owned, app, cutscenes) =>
            {
                owned.LeaseRow<AudioManager>(); // the theatre's field initialiser
                var input = Add<HoldToSkipInput>(owned, "Lucid hold to skip");
                var label = Add<LocalisedUIString>(owned, "Lucid skip label");
                Set(input, "m_labelString", label); // serialized authored data
                var skip = Add<UIContainerSkipCutscene>(owned, "Lucid skip cutscene");
                Set(skip, "m_holdToSkipInput", input);
                Set(skip, "m_theatreSkipString", (Strings)0x999);

                Call(skip, "Start");
                Assert.That(Get<App>(skip, "m_app"), Is.SameAs(app));
                Assert.That(Get<CutsceneManager>(skip, "m_cutsceneManager"), Is.SameAs(cutscenes));
                Assert.That(label.Identifier, Is.EqualTo(Strings.NONE), "no theatre session");

                var theatre = new CutsceneTheatre();
                app.Storage.SetValue(AppFSMKeys.CutsceneTheatre, theatre);
                Call(skip, "Start");
                Assert.That(label.Identifier, Is.EqualTo((Strings)0x999));
            });
        }
    }
}
