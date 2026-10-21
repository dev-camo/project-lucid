using System;
using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime application FSM nodes around CutsceneTheatre: ApplicationStateCutsceneTheatreSetup (TypeDef
    // 0x0200015f), Play (0x0200015e), LoadScene (0x0200015b), UnloadScene (0x02000160) and the HasNext (0x02000168),
    // IsPlaying (0x0200016a) and ShouldLoadScene (0x0200016c) transitions. The theatre is the real CutsceneTheatre, built by
    // its public constructor over a real CutsceneManager, App, DataManager and SaveManager, and handed to the node through
    // the user's FSM storage. Not covered: Play's start of a cutscene, the IsPlaying == true row of the transition, and the
    // load or unload of a loaded scene (all need the Addressables-backed cutscene and scene loading), and the Cleanup
    // state (it plays audio through a live AudioManager and toggles the GUICameraManager singleton's cameras).
    public sealed class CutsceneTheatreFsmNodeTests
    {
        private static readonly FSMUpdateContext s_context = new FSMUpdateContext(0.25f, FSMUpdateType.Manual);

        private sealed class Scene
        {
            public Owned Owned;
            public CutsceneTheatre Theatre;
            public CutsceneManager Manager;
            public DataManager Data;
            public SaveManager Save;
        }

        private static string Name() => "Lucid-theatre-node-" + Guid.NewGuid().ToString("N");

        private static FiniteStateMachine Machine() => new FiniteStateMachine(Name(), skipAddToManager: true);

        private static void SetSerialized(Object target, string field, Action<SerializedProperty> assign)
        {
            var serialized = new SerializedObject(target);
            assign(serialized.FindProperty(field));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WithTheatre(Action<Scene> body) => Owned.Run(owned =>
        {
            // Taken first, so it is released last: it owns the App, DataManager and SaveManager rows.
            owned.Lease(new OwnedCharacterManagers());
            var scene = new Scene { Owned = owned };
            scene.Data = ProcessManager.GetSystem<DataManager>();
            scene.Save = ProcessManager.GetSystem<SaveManager>();

            // The CutsceneManager constructor registers itself and subscribes both actions; its field initialisers
            // create these three rows. It claims its own absent row first, as the constructor publishes into it.
            owned.LeaseActions(SystemAction.Initialise, SystemAction.Shutdown);
            owned.LeaseRow<FullscreenShaderManager>();
            owned.LeaseRow<VisualQualityManager_SDT>();
            owned.LeaseRow<CinemachineCameraManager>();
            var row = owned.Lease(new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<CutsceneManager>()));
            row.Register(null);
            scene.Manager = new CutsceneManager();
            owned.Lease(new ReleaseAction(() => ProcessManager.UnsubscribeFromAllActions(scene.Manager)));
            row.AdoptCurrent();
            owned.LeaseRow<AudioManager>();

            scene.Theatre = new CutsceneTheatre();
            body(scene);
        });

        private static CutsceneDefinition Define(Scene scene, HLCutsceneIdentifier identifier, string guid, string sceneName)
        {
            var definition = scene.Owned.Track(ScriptableObject.CreateInstance<CutsceneDefinition>());
            SetSerialized(definition, "m_guid", property => property.stringValue = guid);
            SetSerialized(definition, "m_metadataFromCharacterPrefabs.AllComicCutscene", property => property.boolValue = true);
            SetSerialized(definition, "m_nonGameplaySceneName", property => property.stringValue = sceneName);
            scene.Data.CutsceneDefinitions[identifier] = definition;
            scene.Save.CurrentSave.AddSeenCutsceneGuid(guid);
            return definition;
        }

        // A theatre whose current cutscene is a definition naming sceneName, as the Setup state leaves it.
        private static void WithCurrentCutscene(string sceneName, Action<Scene> body) => WithTheatre(scene =>
        {
            scene.Theatre.Enqueue(Define(scene, HLCutsceneIdentifier.CS_Intro_A_2D, "lucid-current", sceneName));
            scene.Theatre.DequeueNext();
            Assert.That(scene.Theatre.Cutscene, Is.Not.Null);
            body(scene);
        });

        private static FSMUser TheatreUser(Scene scene)
        {
            var user = new FSMUser(new FSMStorage(0));
            user.Storage.SetValue(AppFSMKeys.CutsceneTheatre, scene.Theatre);
            return user;
        }

        private static IFSMTransition Transition(Func<FiniteStateMachine, FSMIdentifier, string, IFSMTransition> construct, bool trueCausesTransition) =>
            construct(Machine(), Name(), "{\"TrueCausesTransition\":" + (trueCausesTransition ? "true" : "false") + "}");

        // DoUpdate 0x662920: IsEmpty (0x662a86) xor the stored flag (cmp byte [+0x1c], setne, xor at 0x662a8b..0x662a92).
        // The queue state and the flag are each varied: an empty queue fires with the flag clear, a waiting one with it set.
        [Test]
        public void HasNextFiresWhenTheQueueEmptinessDiffersFromTheFlag()
        {
            WithTheatre(scene =>
            {
                FSMUser user = TheatreUser(scene);
                IFSMTransition ifQueued = Transition(ApplicationTransitionCutsceneTheatreHasNext.ConstructInstance, true);
                IFSMTransition ifEmpty = Transition(ApplicationTransitionCutsceneTheatreHasNext.ConstructInstance, false);

                Assert.That(scene.Theatre.IsEmpty(), Is.True);
                Assert.That(ifQueued.Update(user, s_context), Is.False, "empty, true causes the transition");
                Assert.That(ifEmpty.Update(user, s_context), Is.True, "empty, false causes the transition");

                scene.Theatre.Enqueue(Define(scene, HLCutsceneIdentifier.CS_Intro_A_2D, "lucid-queued", ""));
                Assert.That(scene.Theatre.IsEmpty(), Is.False);
                Assert.That(ifQueued.Update(user, s_context), Is.True, "queued, true causes the transition");
                Assert.That(ifEmpty.Update(user, s_context), Is.False, "queued, false causes the transition");
            });
        }

        // DoUpdate 0x662B80: IsPlaying (cmp byte [+0x10], 0 at 0x662ce1) xor the inverted flag (sete at 0x662cec), so the
        // transition fires when the playing state equals the flag. Only the idle theatre is reachable without loading a
        // cutscene: it fires for a clear flag and not for a set one.
        [Test]
        public void IsPlayingFiresWhenThePlayingStateEqualsTheFlag()
        {
            WithTheatre(scene =>
            {
                FSMUser user = TheatreUser(scene);
                Assert.That(scene.Theatre.IsPlaying, Is.False);
                Assert.That(Transition(ApplicationTransitionCutsceneTheatreIsPlaying.ConstructInstance, false).Update(user, s_context), Is.True);
                Assert.That(Transition(ApplicationTransitionCutsceneTheatreIsPlaying.ConstructInstance, true).Update(user, s_context), Is.False);
            });
        }

        // DoUpdate 0x662DD0 (tail 0x663086..0x6630c1): the current cutscene's scene name (+0x68) is tested with
        // IsNullOrWhiteSpace; a named scene is wanted only while absent from LoadedLevels (ContainsKey, then xor 1), and the
        // result is compared with the flag (sete, xor at 0x6630ba..0x6630c1). A blank name never wants a load.
        [Test]
        public void ShouldLoadSceneWantsANamedSceneThatIsNotYetLoaded()
        {
            var scenarios = new (string scene, bool loaded, bool wanted)[]
            {
                ("", false, false),
                ("   ", false, false),
                ("lucid-scene", false, true),
                ("lucid-scene", true, false),
            };
            foreach ((string sceneName, bool loaded, bool wanted) in scenarios)
            {
                WithCurrentCutscene(sceneName, scene =>
                {
                    FSMUser user = TheatreUser(scene);
                    var levels = new Dictionary<string, AsyncOperationHandle>();
                    if (loaded) levels[sceneName] = default;
                    user.Storage.SetValue(AppFSMKeys.LoadedLevels, levels);

                    Assert.That(Transition(ApplicationTransitionCutsceneTheatreShouldLoadScene.ConstructInstance, true).Update(user, s_context),
                        Is.EqualTo(wanted), "'" + sceneName + "' loaded " + loaded + ", flag set");
                    Assert.That(Transition(ApplicationTransitionCutsceneTheatreShouldLoadScene.ConstructInstance, false).Update(user, s_context),
                        Is.EqualTo(!wanted), "'" + sceneName + "' loaded " + loaded + ", flag clear");
                });
            }
        }

        // Setup DoOnEnter 0x661450 calls the base entry, reads the theatre from the user's storage (GetValueOnly, 0x66154f)
        // and tail-calls DequeueNext (0x661563).
        [Test]
        public void SetupMovesTheNextQueuedCutsceneIntoPlace()
        {
            WithTheatre(scene =>
            {
                CutsceneDefinition queued = Define(scene, HLCutsceneIdentifier.CS_Intro_A_2D, "lucid-queued", "");
                scene.Theatre.Enqueue(queued);
                Assert.That(scene.Theatre.Cutscene == null, Is.True);
                FSMUser user = TheatreUser(scene);

                ApplicationStateCutsceneTheatreSetup.ConstructInstance(Machine(), Name(), string.Empty).OnEnter(user, null);

                Assert.That(scene.Theatre.Cutscene, Is.SameAs(queued));
                Assert.That(scene.Theatre.IsEmpty(), Is.True);
            });
        }

        // Play DoOnEnter 0x661290 reads the theatre from the user's storage (0x66137f) and tail-calls CutsceneTheatre.Play
        // (0x66138d), which does nothing without a current cutscene. Starting a cutscene needs the Addressables-backed
        // CutsceneManager load and is not covered.
        [Test]
        public void PlayLeavesATheatreWithoutACutsceneIdle()
        {
            WithTheatre(scene =>
            {
                FSMUser user = TheatreUser(scene);

                ApplicationStateCutsceneTheatrePlay.ConstructInstance(Machine(), Name(), string.Empty).OnEnter(user, null);

                Assert.That(scene.Theatre.IsPlaying, Is.False);
                Assert.That(scene.Theatre.Cutscene == null, Is.True);
            });
        }

        private static string EventJson(ApplicationStateEvent stateEvent) => "{\"EventId\":\"" + stateEvent.name + "\"}";

        private static ApplicationStateEvent NewEvent(Scene scene)
        {
            var stateEvent = scene.Owned.Track(ScriptableObject.CreateInstance<ApplicationStateEvent>());
            stateEvent.name = Name();
            return stateEvent;
        }

        // LoadScene DoOnEnter 0x660960 returns right after IsNullOrWhiteSpace (0x660a75) on the cutscene's scene name,
        // before LoadedLevels is read (0x660b1f) or a coroutine starts: nothing is stored and no state message is published.
        // A named scene starts the Addressables load and is not covered.
        [Test]
        public void LoadSceneDoesNothingForACutsceneWithoutAScene()
        {
            WithCurrentCutscene("", scene =>
            {
                ApplicationStateEvent stateEvent = NewEvent(scene);
                FSMUser user = TheatreUser(scene);
                var events = new List<ApplicationStateEvent>();
                user.Storage.SetValue(AppFSMKeys.StateEvents, events);

                ApplicationStateCutsceneTheatreLoadScene.ConstructInstance(Machine(), Name(), EventJson(stateEvent)).OnEnter(user, null);

                Assert.That(events, Is.Empty);
                Assert.That(user.Storage.TryGetValue(AppFSMKeys.LoadedLevels, out Dictionary<string, AsyncOperationHandle> _), Is.False);
            });
        }

        // UnloadScene DoOnEnter 0x661650: a theatre without a current cutscene (op_Equality at 0x661794), a cutscene without
        // a scene (IsNullOrWhiteSpace at 0x6617e4) and a scene not present in LoadedLevels (TryGetValue at 0x66194c, false
        // branch 0x661984) each publish the state message (PublishMessage 0x66198b) and unload nothing.
        [Test]
        public void UnloadSceneOnlyPublishesWhenThereIsNothingToUnload()
        {
            WithTheatre(scene =>
            {
                ApplicationStateEvent stateEvent = NewEvent(scene);
                FSMUser user = TheatreUser(scene);
                var events = new List<ApplicationStateEvent>();
                user.Storage.SetValue(AppFSMKeys.StateEvents, events);
                ApplicationStateCutsceneTheatreUnloadScene.ConstructInstance(Machine(), Name(), EventJson(stateEvent)).OnEnter(user, null);
                Assert.That(events, Is.EqualTo(new[] { stateEvent }), "no current cutscene");
            });
            WithCurrentCutscene("", scene =>
            {
                ApplicationStateEvent stateEvent = NewEvent(scene);
                FSMUser user = TheatreUser(scene);
                var events = new List<ApplicationStateEvent>();
                user.Storage.SetValue(AppFSMKeys.StateEvents, events);
                ApplicationStateCutsceneTheatreUnloadScene.ConstructInstance(Machine(), Name(), EventJson(stateEvent)).OnEnter(user, null);
                Assert.That(events, Is.EqualTo(new[] { stateEvent }), "blank scene name");
            });
            WithCurrentCutscene("lucid-scene", scene =>
            {
                ApplicationStateEvent stateEvent = NewEvent(scene);
                FSMUser user = TheatreUser(scene);
                var events = new List<ApplicationStateEvent>();
                user.Storage.SetValue(AppFSMKeys.StateEvents, events);
                var levels = new Dictionary<string, AsyncOperationHandle>();
                user.Storage.SetValue(AppFSMKeys.LoadedLevels, levels);
                ApplicationStateCutsceneTheatreUnloadScene.ConstructInstance(Machine(), Name(), EventJson(stateEvent)).OnEnter(user, null);
                Assert.That(events, Is.EqualTo(new[] { stateEvent }), "scene not loaded");
                Assert.That(levels, Is.Empty);
            });
        }

        // UnloadScene DoOnEnter 0x661650 (0x6617a2..0x6617eb): when the next queued cutscene names the same non-blank scene
        // (Peek, op_Inequality, IsNullOrWhiteSpace, String.op_Equality), the loaded scene is kept: the message is published
        // and the LoadedLevels entry stays, where the unload path would remove it.
        [Test]
        public void UnloadSceneKeepsASceneTheNextCutsceneShares()
        {
            WithTheatre(scene =>
            {
                Define(scene, HLCutsceneIdentifier.CS_Intro_A_2D, "lucid-first", "lucid-shared");
                Define(scene, HLCutsceneIdentifier.CS_Intro_B_2D, "lucid-second", "lucid-shared");
                scene.Theatre.EnqueueAll();
                scene.Theatre.DequeueNext();
                Assert.That(scene.Theatre.Peek(), Is.Not.Null);

                ApplicationStateEvent stateEvent = NewEvent(scene);
                FSMUser user = TheatreUser(scene);
                var events = new List<ApplicationStateEvent>();
                user.Storage.SetValue(AppFSMKeys.StateEvents, events);
                var levels = new Dictionary<string, AsyncOperationHandle> { { "lucid-shared", default } };
                user.Storage.SetValue(AppFSMKeys.LoadedLevels, levels);

                ApplicationStateCutsceneTheatreUnloadScene.ConstructInstance(Machine(), Name(), EventJson(stateEvent)).OnEnter(user, null);

                Assert.That(events, Is.EqualTo(new[] { stateEvent }));
                Assert.That(levels.ContainsKey("lucid-shared"), Is.True, "the shared scene stays loaded");
            });
        }
    }
}
