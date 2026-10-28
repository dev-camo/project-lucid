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
    // its save binding on each rerun.
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
    }
}
