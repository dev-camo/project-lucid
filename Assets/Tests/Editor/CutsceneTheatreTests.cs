using System;
using System.Linq;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime CutsceneTheatre (TypeDef 0x0200043f, x86-64 0x7A3DC0..0x7A4C00). Its collaborators are real:
    // the App and DataManager registered by OwnedCharacterManagers (with an open SaveManager), a CutsceneManager built
    // by its real constructor into its absent row, and optionally an AudioManager holding a UI background source. The
    // theatre itself is built by its public constructor. Play's positive path would load the cutscene prefab through
    // the CutsceneManager (addressables) and is not covered; the skipped-cutscene check reads a cutscene manager
    // whose current cutscene is a real ComicCutscene handed over directly, because playing one needs that load.
    public sealed class CutsceneTheatreTests
    {
        private sealed class Scene
        {
            public Owned Owned;
            public CutsceneTheatre Theatre;
            public CutsceneManager Manager;
            public App App;
            public DataManager Data;
            public SaveManager Save;
            public HLAudioSourceData UiBackground;
        }

        private static void SetSerialized(Object target, string field, Action<SerializedProperty> assign)
        {
            var serialized = new SerializedObject(target);
            assign(serialized.FindProperty(field));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WithTheatre(Action<Scene> body, bool withAudio = false) => Owned.Run(owned =>
        {
            // Taken first, so it is released last: it owns the App, DataManager and SaveManager rows.
            owned.Lease(new OwnedCharacterManagers());
            var scene = new Scene { Owned = owned };
            scene.App = ProcessManager.GetSystem<App>();
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

            if (withAudio)
            {
                GameObject host = owned.Host("Lucid owned audio manager", false);
                var audio = host.AddComponent<AudioManager>();
                var handler = host.AddComponent<HLAudioSourceHandler>();
                SetSerialized(audio, "m_audioSourceHandler", property => property.objectReferenceValue = handler);
                handler.Initialise(audio.transform);
                owned.Register(audio);
                scene.UiBackground = new HLAudioSourceData(HLAudioSourceIdentifier.UiBackground, host.AddComponent<AudioSource>(),
                    false, HLAudioSourceTransition.None, default);
                audio.AddSource(scene.UiBackground);
                scene.UiBackground.SetAudioClip(null, HLAudioClipIdentifier.AmbientBossCrabOcean, false);
            }
            else owned.LeaseRow<AudioManager>();

            scene.Theatre = new CutsceneTheatre();
            body(scene);
        });

        private static CutsceneDefinition Define(Scene scene, HLCutsceneIdentifier identifier, string guid, bool validInTheatre, bool seen)
        {
            var definition = scene.Owned.Track(ScriptableObject.CreateInstance<CutsceneDefinition>());
            SetSerialized(definition, "m_guid", property => property.stringValue = guid);
            SetSerialized(definition, "m_metadataFromCharacterPrefabs.AllComicCutscene", property => property.boolValue = validInTheatre);
            scene.Data.CutsceneDefinitions[identifier] = definition;
            if (seen) scene.Save.CurrentSave.AddSeenCutsceneGuid(guid);
            return definition;
        }

        private static bool HoldsTheatreKey(Scene scene) => scene.App.Storage.HasValue(AppFSMKeys.CutsceneTheatre);

        private static bool Subscribed(Scene scene) =>
            Get<Action>(scene.Manager, "OnCutsceneFinished").GetInvocationList().Any(handler => ReferenceEquals(handler.Target, scene.Theatre));

        // Constructor 0x7A3E30: the queue starts empty, nothing is playing, and (CutsceneManager already valid, so
        // InvokeOnValid runs at once, 0x7a4030) the finished handler is attached to the manager's event.
        [Test]
        public void ConstructionStartsEmptyAndSubscribesToTheManagersFinishedEvent()
        {
            WithTheatre(scene =>
            {
                Assert.That(scene.Theatre.IsEmpty(), Is.True);
                Assert.That(scene.Theatre.Peek(), Is.Null);
                Assert.That(scene.Theatre.IsPlaying, Is.False);
                Assert.That(scene.Theatre.Cutscene == null, Is.True);
                Assert.That((int)scene.Theatre.PreviousMusic, Is.EqualTo(0));
                Assert.That(Subscribed(scene), Is.True);
            });
        }

        // Enqueue 0x7A42C0: only an empty queue outside a running theatre session (storage key absent, 0x7a4392)
        // accepts a cutscene; it then claims the key with the theatre itself (SetValue<CutsceneTheatre>, 0x7a44c9).
        // A second cutscene is refused while the first waits, and so is any cutscene while the key remains.
        [Test]
        public void EnqueueTakesOnlyTheFirstCutsceneAndClaimsTheSessionKey()
        {
            WithTheatre(scene =>
            {
                CutsceneDefinition first = Define(scene, HLCutsceneIdentifier.CS_Intro_A_2D, "lucid-first", true, true);
                CutsceneDefinition second = Define(scene, HLCutsceneIdentifier.CS_Intro_B_2D, "lucid-second", true, true);
                Assert.That(HoldsTheatreKey(scene), Is.False);
                scene.Theatre.Enqueue(first);
                Assert.That(scene.Theatre.IsEmpty(), Is.False);
                Assert.That(scene.Theatre.Peek(), Is.SameAs(first));
                Assert.That(scene.App.Storage.TryGetValue(AppFSMKeys.CutsceneTheatre, out CutsceneTheatre stored), Is.True);
                Assert.That(stored, Is.SameAs(scene.Theatre));

                scene.Theatre.Enqueue(second);
                scene.Theatre.DequeueNext();
                Assert.That(scene.Theatre.Cutscene, Is.SameAs(first));
                Assert.That(scene.Theatre.IsEmpty(), Is.True, "the second cutscene was refused");

                scene.Theatre.Enqueue(second);
                Assert.That(scene.Theatre.IsEmpty(), Is.True, "the session key is still held");
            });
        }

        // 0x7A44E0 (inlined at 0x7a43b6..0x7a4400): the music playing on the UI background source (CurrentAudio, +0x30) is
        // remembered when the session starts, and the property stays at None without an audio manager.
        [Test]
        public void EnqueueRemembersTheUiBackgroundMusic()
        {
            WithTheatre(scene =>
            {
                scene.Theatre.Enqueue(Define(scene, HLCutsceneIdentifier.CS_Intro_A_2D, "lucid-first", true, true));
                Assert.That(scene.Theatre.PreviousMusic, Is.EqualTo(HLAudioClipIdentifier.AmbientBossCrabOcean));
            }, withAudio: true);
            WithTheatre(scene =>
            {
                scene.Theatre.Enqueue(Define(scene, HLCutsceneIdentifier.CS_Intro_A_2D, "lucid-first", true, true));
                Assert.That(scene.Theatre.PreviousMusic, Is.EqualTo(HLAudioClipIdentifier.None));
            });
        }

        // EnqueueAll 0x7A4560: with an open save and no running session it queues every definition that is valid in the
        // theatre (the AllComicCutscene byte at +0x30) and already seen (the save's SeenCutsceneGuids contains the
        // definition's GUID), in the dictionary's order, and claims the session key.
        [Test]
        public void EnqueueAllQueuesTheSeenTheatreCutscenesInOrderAndClaimsTheKey()
        {
            WithTheatre(scene =>
            {
                CutsceneDefinition first = Define(scene, HLCutsceneIdentifier.CS_Intro_A_2D, "lucid-a", true, true);
                Define(scene, HLCutsceneIdentifier.CS_Intro_B_2D, "lucid-b", true, false);
                Define(scene, HLCutsceneIdentifier.CS_End_Credits, "lucid-c", false, true);
                CutsceneDefinition last = Define(scene, HLCutsceneIdentifier.CS_CZ_Purple_Shadow_Tutorial_Challenge, "lucid-d", true, true);
                scene.Theatre.EnqueueAll();
                Assert.That(HoldsTheatreKey(scene), Is.True);
                Assert.That(scene.Theatre.Peek(), Is.SameAs(first));
                scene.Theatre.DequeueNext();
                Assert.That(scene.Theatre.Cutscene, Is.SameAs(first));
                Assert.That(scene.Theatre.Peek(), Is.SameAs(last));
                scene.Theatre.DequeueNext();
                Assert.That(scene.Theatre.Cutscene, Is.SameAs(last));
                Assert.That(scene.Theatre.IsEmpty(), Is.True);
            });
        }

        // 0x7A4560: a running session (key held, 0x7a46c3) or a waiting queue (0x7a4675) refuses the whole call.
        [Test]
        public void EnqueueAllDoesNothingWhileASessionIsRunning()
        {
            WithTheatre(scene =>
            {
                CutsceneDefinition only = Define(scene, HLCutsceneIdentifier.CS_Intro_A_2D, "lucid-a", true, true);
                scene.Theatre.Enqueue(only);
                scene.Theatre.DequeueNext();
                Assert.That(scene.Theatre.IsEmpty(), Is.True);
                scene.Theatre.EnqueueAll();
                Assert.That(scene.Theatre.IsEmpty(), Is.True, "the key is still held");
            });
        }

        // DequeueNext 0x7A4940 needs a valid manager with no current cutscene (Unity null, 0x7a49e8) and a non-empty
        // queue; a playing cutscene leaves the queue untouched.
        [Test]
        public void DequeueNextWaitsWhileACutsceneIsPlayingAndKeepsTheQueueOrder()
        {
            WithTheatre(scene =>
            {
                CutsceneDefinition first = Define(scene, HLCutsceneIdentifier.CS_Intro_A_2D, "lucid-a", true, true);
                scene.Theatre.DequeueNext();
                Assert.That(scene.Theatre.Cutscene == null, Is.True, "nothing queued");
                scene.Theatre.Enqueue(first);
                ComicCutscene playing = scene.Owned.Host("Lucid playing cutscene", false).AddComponent<ComicCutscene>();
                Set(scene.Manager, "m_currentCutscene", playing); // Reflection: playing a cutscene needs the addressables load
                scene.Theatre.DequeueNext();
                Assert.That(scene.Theatre.Cutscene == null, Is.True, "a cutscene is playing");
                Assert.That(scene.Theatre.IsEmpty(), Is.False);
                Set(scene.Manager, "m_currentCutscene", null);
                scene.Theatre.DequeueNext();
                Assert.That(scene.Theatre.Cutscene, Is.SameAs(first));
            });
        }

        // Play 0x7A4A30: without a chosen cutscene (ScriptableObjectWithGuid.op_Equality, 0x7a4a80) nothing is loaded
        // and the theatre is not marked playing.
        [Test]
        public void PlayWithoutAChosenCutsceneDoesNothing()
        {
            WithTheatre(scene =>
            {
                scene.Theatre.Play();
                Assert.That(scene.Theatre.IsPlaying, Is.False);
                Assert.That(scene.Manager.CutsceneIsLoading, Is.False);
            });
        }

        // OnCutsceneFinished 0x7A4B30 (subscribed to the manager): the theatre stops playing, and a skipped current
        // cutscene (WasCutsceneSkipped, 0x7a4bc7: a current cutscene whose skipped flag is set) abandons the queue.
        [Test]
        public void ASkippedCutsceneEmptiesTheQueueWhenTheManagerReportsItFinished()
        {
            foreach (bool skipped in new[] { true, false })
            {
                WithTheatre(scene =>
                {
                    scene.Theatre.Enqueue(Define(scene, HLCutsceneIdentifier.CS_Intro_A_2D, "lucid-a", true, true));
                    ComicCutscene finished = scene.Owned.Host("Lucid finished cutscene", false).AddComponent<ComicCutscene>();
                    finished.WasSkipped = skipped;
                    Set(scene.Manager, "m_currentCutscene", finished); // Reflection: playing a cutscene needs the addressables load
                    Get<Action>(scene.Manager, "OnCutsceneFinished")();
                    Assert.That(scene.Theatre.IsPlaying, Is.False);
                    Assert.That(scene.Theatre.IsEmpty(), Is.EqualTo(skipped), "skipped " + skipped);
                });
            }
        }

        // Cleanup 0x7A4120: detaches the finished handler, and clears the playing flag, the chosen cutscene and the
        // queue - but not the session key in the App's storage.
        [Test]
        public void CleanupDetachesAndClearsEverythingExceptTheSessionKey()
        {
            WithTheatre(scene =>
            {
                scene.Theatre.Enqueue(Define(scene, HLCutsceneIdentifier.CS_Intro_A_2D, "lucid-a", true, true));
                scene.Theatre.Enqueue(Define(scene, HLCutsceneIdentifier.CS_Intro_B_2D, "lucid-b", true, true));
                scene.Theatre.DequeueNext();
                Assert.That(scene.Theatre.Cutscene == null, Is.False);
                Assert.That(Subscribed(scene), Is.True);
                scene.Theatre.Cleanup();
                Assert.That(Subscribed(scene), Is.False);
                Assert.That(scene.Theatre.Cutscene == null, Is.True);
                Assert.That(scene.Theatre.IsPlaying, Is.False);
                Assert.That(scene.Theatre.IsEmpty(), Is.True);
                Assert.That(HoldsTheatreKey(scene), Is.True, "Cleanup leaves the key for the owner to remove");
            });
        }
    }
}
