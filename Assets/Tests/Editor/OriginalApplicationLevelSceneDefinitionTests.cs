using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Three application FSM nodes read the current level's SceneDefinition (LevelManagerLevel +0x18), not its
    // LevelDefinition (+0x10), in 1.10.1: get_LevelDefinition 0x5975D0 is "mov 0x10(%rdi)" and get_SceneDefinition
    // 0x5975E0 is "mov 0x18(%rdi)". Every case builds a level whose two definitions differ (LevelDefinition A,
    // SceneDefinition B, as LevelManager.LoadLevelSceneForFSM stores them when a mission overrides the scene), so a
    // node that reads the wrong one gives the opposite answer. The expected answers are the native predicates
    // restated from the disassembly, not read back from the implementation.
    public sealed class OriginalApplicationLevelSceneDefinitionTests
    {
        private const string GuidA = "lucid-level-definition-A";
        private const string GuidB = "lucid-scene-definition-B";
        private static readonly FSMUpdateContext s_context = new FSMUpdateContext(0.25f, FSMUpdateType.Manual);

        private static string Name() => "Lucid-level-scene-" + Guid.NewGuid().ToString("N");
        private static FiniteStateMachine Machine() => new FiniteStateMachine(Name(), skipAddToManager: true);

        private static GameplayLevelDefinition Definition(Owned owned, string guid)
        {
            var definition = owned.Track(ScriptableObject.CreateInstance<GameplayLevelDefinition>());
            Set(definition, "m_guid", guid); // Reflection: authored GUID of a serialized definition
            return definition;
        }

        // An unawakened LevelManager registered in its row, whose current level is (levelDefinition, sceneDefinition).
        private static LevelManager Level(Owned owned, GameplayLevelDefinition levelDefinition, GameplayLevelDefinition sceneDefinition, bool valid)
        {
            owned.LeaseRow<AudioManager>();
            owned.LeaseRow<SaveManager>();
            owned.LeaseRow<MissionManager>();
            var levelManager = owned.Host("Lucid level manager", false).AddComponent<LevelManager>();
            typeof(LevelManager).GetProperty("CurrentLevelValid").SetValue(levelManager, valid);
            Set(levelManager, "m_currentLevel", new LevelManagerLevel(levelDefinition, sceneDefinition));
            return owned.Register(levelManager);
        }

        private static App NewApp(Owned owned)
        {
            owned.LeaseRow<UIScreenTransitionManager>();
            return owned.Host("Lucid app", false).AddComponent<App>();
        }

        private static ApplicationStateEvent NewEvent(Owned owned)
        {
            var stateEvent = owned.Track(ScriptableObject.CreateInstance<ApplicationStateEvent>());
            stateEvent.name = Name();
            return stateEvent;
        }

        // ApplicationStateLoadLevelEnsureOneLevel MoveNext 0x06000799, x86-64 0x658545: currentLevel+0x18 is compared
        // with parameters+0x10 by ScriptableObjectWithGuid.op_Equality 0x65854f and the result is inverted; a null
        // parameters object takes 0x65857f (different = true); m_forceUnloadAny (+0x30) is ORed in. One MoveNext yields
        // the unstarted UnloadLevel iterator when an unload is requested, and otherwise publishes and finishes.
        private static bool EnsureOneLevelUnloads(Owned owned, bool forceUnloadAny, GameplayLevelDefinition parametersDefinition,
            bool nullParameters)
        {
            GameplayLevelDefinition a = Definition(owned, GuidA);
            GameplayLevelDefinition b = Definition(owned, GuidB);
            Level(owned, a, b, true);
            ApplicationStateEvent stateEvent = NewEvent(owned);
            IFSMState state = ApplicationStateLoadLevelEnsureOneLevel.ConstructInstance(Machine(), Name(),
                "{\"EventId\":\"" + stateEvent.name + "\",\"ForceUnloadAny\":" + (forceUnloadAny ? "true" : "false") + "}");
            var user = new FSMUser(new FSMStorage(0));
            var events = new List<ApplicationStateEvent>();
            user.Storage.SetValue(AppFSMKeys.StateEvents, events);
            if (!nullParameters)
                user.Storage.SetValue(AppFSMKeys.LevelLoadParameters, new LevelManagerLoadLevelParameters(parametersDefinition));
            var routine = (IEnumerator)typeof(ApplicationStateLoadLevelEnsureOneLevel)
                .GetMethod("EnsureOneLevel", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(state, new object[] { user });
            bool unloads = routine.MoveNext();
            Assert.That(events.Count, Is.EqualTo(unloads ? 0 : 1), "the event is published only when no unload is yielded");
            return unloads;
        }

        // The level is (A, B). Parameters naming the scene B are the same level, so nothing unloads.
        [Test]
        public void EnsureOneLevelKeepsALevelWhoseSceneDefinitionEqualsTheParameters()
        {
            Owned.Run(owned =>
            {
                GameplayLevelDefinition probe = Definition(owned, GuidB);
                Assert.That(EnsureOneLevelUnloads(owned, false, probe, false), Is.False);
            });
        }

        // Parameters naming only the LevelDefinition A are a different level: the native compares +0x18 (B), not +0x10 (A).
        [Test]
        public void EnsureOneLevelUnloadsWhenTheParametersEqualOnlyTheLevelDefinition()
        {
            Owned.Run(owned =>
            {
                GameplayLevelDefinition probe = Definition(owned, GuidA);
                Assert.That(EnsureOneLevelUnloads(owned, false, probe, false), Is.True);
            });
        }

        [Test]
        public void EnsureOneLevelForceUnloadAnyAndNullParametersSelectTheUnload()
        {
            Owned.Run(owned =>
            {
                GameplayLevelDefinition probe = Definition(owned, GuidB);
                Assert.That(EnsureOneLevelUnloads(owned, true, probe, false), Is.True, "ForceUnloadAny with matching parameters");
            });
            Owned.Run(owned => Assert.That(EnsureOneLevelUnloads(owned, false, null, true), Is.True, "null parameters"));
            Owned.Run(owned => Assert.That(EnsureOneLevelUnloads(owned, true, null, true), Is.True, "both"));
        }

        // ApplicationStateLoadLevelProcess.DoOnEnter 0x0600079f: the stored last-loaded GUID is compared
        // (String.op_Inequality 0x658821) with the GUID read through currentLevel+0x18 (0x658818), ORed with
        // !HasSystemOfType<TrackManager>. The replace branch reads level.Data (+0x20), null here, so reaching it
        // faults with a NullReferenceException raised outside SetLevelActiveForFSM; the other branch goes on to
        // SetLevelActiveForFSM, where the default Addressables handle throws System.Exception (invalid operation handle).
        // Each branch is told apart by where the exception was raised, and any other failure fails the test.
        private static bool LoadLevelProcessReplaces(Owned owned, string lastLoadedGuid, bool trackManagerPresent)
        {
            GameplayLevelDefinition a = Definition(owned, GuidA);
            GameplayLevelDefinition b = Definition(owned, GuidB);
            LevelManager levelManager = Level(owned, a, b, true);
            typeof(LevelManager).GetProperty("ManagedSystems").SetValue(levelManager, new LevelManagerSystems(null));
            if (trackManagerPresent)
                Get<Dictionary<Type, ISystem>>(levelManager.ManagedSystems, "m_managedSystems")[typeof(TrackManager)] = null;
            App app = NewApp(owned);
            app.Storage.SetValue(AppFSMKeys.LastLevelLoadedGUID, lastLoadedGuid);
            ApplicationStateEvent stateEvent = NewEvent(owned);
            IFSMState state = ApplicationStateLoadLevelProcess.ConstructInstance(Machine(), Name(), "{\"EventId\":\"" + stateEvent.name + "\"}");
            try { state.OnEnter(app, null); }
            catch (Exception exception)
            {
                if (exception.StackTrace.Contains("SetLevelActiveForFSM"))
                    return false;
                if (exception is NullReferenceException)
                    return true;
                throw;
            }
            throw new AssertionException("OnEnter completed without reaching either faulting branch");
        }

        [Test]
        public void LoadLevelProcessKeepsTheTrackManagerWhenTheLastLoadedGuidIsTheSceneGuid()
        {
            Owned.Run(owned => Assert.That(LoadLevelProcessReplaces(owned, GuidB, true), Is.False));
        }

        [Test]
        public void LoadLevelProcessReplacesTheTrackManagerWhenTheLastLoadedGuidIsOnlyTheLevelGuid()
        {
            Owned.Run(owned => Assert.That(LoadLevelProcessReplaces(owned, GuidA, true), Is.True));
        }

        [Test]
        public void LoadLevelProcessReplacesAMissingTrackManagerEvenForTheSceneGuid()
        {
            Owned.Run(owned => Assert.That(LoadLevelProcessReplaces(owned, GuidB, false), Is.True));
        }

        // ApplicationTransitionMetaGameIsSameLevel.DoUpdate 0x0600088a: with the flag set, op_Equality (0x665ec1) of
        // currentLevel+0x18 and the resolved definition; with it clear, op_Inequality (0x665edd). No valid level gives
        // the negated flag.
        private static bool IsSameLevel(Owned owned, bool trueWhenSameLevel, string metaGameGuid, bool levelValid)
        {
            GameplayLevelDefinition a = Definition(owned, GuidA);
            GameplayLevelDefinition b = Definition(owned, GuidB);
            Level(owned, a, b, levelValid);
            owned.LeaseActions(SystemAction.Initialise, SystemAction.Shutdown);
            owned.LeaseRow<GameplayIslandManager>();
            owned.LeaseRow<CharacterManager>();
            var missions = new MissionManager();
            owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() => ProcessManager.UnsubscribeFromAllActions(missions)));
            owned.RegisterIntoLeasedRow(missions);
            App app = NewApp(owned);
            app.Storage.SetValue(AppFSMKeys.MetaGameState, new MetaGameState(metaGameGuid == GuidA ? a : b, null));
            IFSMTransition transition = ApplicationTransitionMetaGameIsSameLevel.ConstructInstance(Machine(), Name(),
                "{\"TrueWhenSameLevel\":" + (trueWhenSameLevel ? "true" : "false") + "}");
            return transition.Update(app, s_context);
        }

        // The meta-game names scene B (A is only the level definition): the same level.
        [Test]
        public void IsSameLevelTreatsTheSceneDefinitionAsTheCurrentLevel()
        {
            Owned.Run(owned => Assert.That(IsSameLevel(owned, true, GuidB, true), Is.True));
            Owned.Run(owned => Assert.That(IsSameLevel(owned, false, GuidB, true), Is.False));
        }

        // The meta-game names A, which is only the LevelDefinition: a different level.
        [Test]
        public void IsSameLevelTreatsAMatchingLevelDefinitionAsADifferentLevel()
        {
            Owned.Run(owned => Assert.That(IsSameLevel(owned, true, GuidA, true), Is.False));
            Owned.Run(owned => Assert.That(IsSameLevel(owned, false, GuidA, true), Is.True));
        }

        [Test]
        public void IsSameLevelWithoutACurrentLevelReturnsTheNegatedFlag()
        {
            Owned.Run(owned => Assert.That(IsSameLevel(owned, true, GuidB, false), Is.False));
            Owned.Run(owned => Assert.That(IsSameLevel(owned, false, GuidB, false), Is.True));
        }
    }
}
