using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Offline hooks H-032/H-036 keep the original persistent OnSystemStartup subscriptions of Achievements: the original
    // AddCollectableAchievements (0x060005a6) ends with SystemRef<EnemyManager>.add_OnSystemStartup (0x535cf0) and
    // AddEnemyAchievements (0x060005ac) with SystemRef<CharacterManager>.add_OnSystemStartup (0x537570). Each gameplay
    // session registers new level-managed Enemy and Character managers, and the chain binds to them again. H-038 replaces
    // its save binding on each rerun. The original AddMissionAchievements (0x060005a9 @0x535FF0) subscribes MissionCompleted
    // with System.Delegate::Combine (call at 0x53630f, field 0x38 of the mission manager) straight after its zone-count check
    // (IReadOnlyCollection.get_Count at 0x5362c4, return when zero at 0x5362c8), with no duplicate check, so every rerun adds
    // one more subscription to the application-scoped MissionManager; H-084 lets the offline stop remove them all.
    public sealed class OfflineAchievementStartupRebindTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static int Subscriptions<T>(string handler) where T : class, ISystem
        {
            var startup = (Delegate)typeof(SystemRef<T>).GetField("OnSystemStartup", Members).GetValue(ProcessManager.GetSystemRef<T>(null, true));
            return startup == null ? 0 : startup.GetInvocationList().Count(entry => entry.Method.Name == handler);
        }

        private static ISaveGameListener[] Bindings(SaveManager save) =>
            Get<System.Collections.Generic.List<ISaveGameListener>>(save, "m_saveGameListeners")
                .Where(listener => listener.GetType().FullName == "ProjectLucid.Offline.LocalAchievementSaveBinding").ToArray();

        [Test]
        public void EachGameplaySessionRebindsTheEnemyAndCharacterManagers()
        {
            Assert.That(ProjectLucid.Offline.OfflineAdapters.Active, Is.True, "Owned offline case requires the offline adapters.");
            Owned.Run(owned =>
            {
                // The character fixture registers the SaveManager, with an open save its managers' constructors read.
                OwnedCharacterManagers characters = owned.Lease(new OwnedCharacterManagers());
                owned.LeaseRow<GameplayIslandManager>();
                owned.LeaseRow<LevelManager>();
                owned.LeaseRow<EnemyManager>();
                owned.LeaseRow<CharacterManager>();
                CharacterManager[] sessionCharacters = { characters.NewManager(), characters.NewManager() };
                // Then the save is closed for the sessions, so each binding waits and no achievement is bound.
                SaveManager save = ProcessManager.GetSystem<SaveManager>();
                object openSave = Get<object>(save, "m_currentSave");
                Set(save, "m_currentSave", null);
                owned.Lease(new ReleaseAction(() => Set(save, "m_currentSave", openSave)));
                var achievements = new Achievements();
                // Released first, and also if an assertion fails before the stop below: the sessions' managers leave
                // their rows, then any startup subscription the chain made and the binding go.
                owned.Lease(new ReleaseAction(() =>
                {
                    Call(achievements, "StopOfflineAchievementSubscriptions");
                    var enemyStartup = (Action<EnemyManager>)Delegate.CreateDelegate(typeof(Action<EnemyManager>), achievements, "AddEnemyAchievements");
                    var characterStartup = (Action<CharacterManager>)Delegate.CreateDelegate(typeof(Action<CharacterManager>), achievements, "AddCharacterAchievements");
                    while (Subscriptions<EnemyManager>("AddEnemyAchievements") > 0) ProcessManager.GetSystemRef<EnemyManager>(null, true).OnSystemStartup -= enemyStartup;
                    while (Subscriptions<CharacterManager>("AddCharacterAchievements") > 0) ProcessManager.GetSystemRef<CharacterManager>(null, true).OnSystemStartup -= characterStartup;
                    ProcessManager.UnregisterSystem<CharacterManager>();
                    ProcessManager.UnregisterSystem<EnemyManager>();
                }));

                Call(achievements, "AddCollectableAchievements", new object[] { null });
                Assert.That(Subscriptions<EnemyManager>("AddEnemyAchievements"), Is.EqualTo(1), "a persistent enemy startup subscription");

                ISaveGameListener firstBinding = null;
                for (int session = 1; session <= 2; session++)
                {
                    ProcessManager.UnregisterSystem<CharacterManager>();
                    ProcessManager.UnregisterSystem<EnemyManager>();
                    var enemies = new EnemyManager();
                    owned.Lease(new ReleaseAction(() => ProcessManager.UnsubscribeFromAllActions(enemies)));
                    ProcessManager.RegisterSystem(enemies);
                    Assert.That(Get<EnemyManager>(achievements, "m_enemyManager"), Is.SameAs(enemies), "session " + session);
                    Assert.That(Subscriptions<CharacterManager>("AddCharacterAchievements"), Is.EqualTo(session),
                        "one more character startup subscription per session, as in the original");

                    CharacterManager characterManager = sessionCharacters[session - 1];
                    ProcessManager.RegisterSystem(characterManager);
                    Assert.That(Get<CharacterManager>(achievements, "m_characterManager"), Is.SameAs(characterManager), "session " + session);
                    Assert.That(Get<SaveManager>(achievements, "m_saveManager"), Is.SameAs(save));

                    ISaveGameListener[] bindings = Bindings(save);
                    Assert.That(bindings.Length, Is.EqualTo(1), "each rerun replaces the save binding");
                    Assert.That(bindings[0], Is.SameAs(Get<object>(achievements, "m_offlineSaveBinding")));
                    if (session == 1) firstBinding = bindings[0];
                    else Assert.That(bindings[0], Is.Not.SameAs(firstBinding));
                }

                // The offline stop removes every startup subscription the chain made, the repeated character ones included.
                Call(achievements, "StopOfflineAchievementSubscriptions");
                Assert.That(Subscriptions<CharacterManager>("AddCharacterAchievements"), Is.EqualTo(0), "no character startup subscription survives the stop");
                Assert.That(Subscriptions<EnemyManager>("AddEnemyAchievements"), Is.EqualTo(0), "no enemy startup subscription survives the stop");
            });
        }

        [Test]
        public void EachGameplaySessionAddsAMissionCompletedSubscriptionAndTheStopRemovesThemAll()
        {
            Assert.That(ProjectLucid.Offline.OfflineAdapters.Active, Is.True, "Owned offline case requires the offline adapters.");
            // An open save, a real LevelManager whose authored GameLevels holds a zone, and a real MissionManager.
            LevelRequirementWorld.Run((owned, world) =>
            {
                world.AddZone("lucid-zone", world.NewLevel("lucid-level", false));
                Assert.That(world.GameLevels.GetZones().Count, Is.EqualTo(1));
                Assert.That(world.Save.CurrentSave, Is.Not.Null, "the save is open");
                LevelManager levelManager = ProcessManager.GetSystem<LevelManager>();
                var achievements = new Achievements();
                // Released first, also if an assertion fails: the stop, then any subscription it did not remove.
                owned.Lease(new ReleaseAction(() =>
                {
                    Call(achievements, "StopOfflineAchievementSubscriptions");
                    var completed = (Action<MissionState>)Delegate.CreateDelegate(typeof(Action<MissionState>), achievements, "MissionCompleted");
                    while (MissionCompletedSubscriptions(world.Missions, achievements) > 0) world.Missions.OnMissionCompleted -= completed;
                }));

                for (int session = 1; session <= 2; session++)
                {
                    // Called directly rather than through the save binding and AddSaveDataAchievements, which need the enemy,
                    // collectable and character managers too. The mission manager exists, so AddLevelAchievements reaches AddMissionAchievements and subscribes
                    // MissionCompleted. Only the achievement behaviours that follow need a ready local AchievementsManager
                    // lease, which this fixture does not build, so the first one throws; the subscription is already made.
                    Assert.Throws<NullReferenceException>(() => Call(achievements, "AddLevelAchievements", levelManager), "session " + session);
                    Assert.That(MissionCompletedSubscriptions(world.Missions, achievements), Is.EqualTo(session),
                        "one more MissionCompleted subscription per session, as in the original");
                }

                Call(achievements, "StopOfflineAchievementSubscriptions");
                Assert.That(MissionCompletedSubscriptions(world.Missions, achievements), Is.EqualTo(0), "no MissionCompleted subscription survives the stop");
            });
        }

        // MissionManager adds handlers of its own to the same field, so count only this instance's MissionCompleted.
        private static int MissionCompletedSubscriptions(MissionManager missions, Achievements achievements) =>
            missions.OnMissionCompleted == null ? 0 : missions.OnMissionCompleted.GetInvocationList()
                .Count(entry => entry.Method.Name == "MissionCompleted" && ReferenceEquals(entry.Target, achievements));
    }
}
