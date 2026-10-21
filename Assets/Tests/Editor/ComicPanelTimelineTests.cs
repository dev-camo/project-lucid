using System;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime ComicPanelClip (TypeDef 0x0200042e, x86-64 0x79DE90..0x79E2B0), ComicPanelMixer (0x0200042f,
    // 0x79E2D0..0x79EA40) and ComicPanelTrack (0x02000430, 0x79EA50..0x79EB50). Playable graphs, clips, tracks and the
    // comic cutscene are real objects on owned hosts. Application.isPlaying is false here, so the mixer's play-mode
    // registration with the CutsceneManager in OnGraphStart (0x79e396 onwards) is not covered; ProcessFrame takes the
    // edit-mode branch (0x79e64f), which skips nothing the retail build keeps, and its comic cutscene is handed over
    // directly.
    public sealed class ComicPanelTimelineTests
    {
        private static void WithGraph(Owned owned, Action<PlayableGraph> body)
        {
            PlayableGraph graph = PlayableGraph.Create("Lucid comic panel graph");
            owned.Lease(new ReleaseAction(() => { if (graph.IsValid()) graph.Destroy(); }));
            body(graph);
        }

        private static void SetSerialized(Object target, string field, Action<SerializedProperty> assign)
        {
            var serialized = new SerializedObject(target);
            assign(serialized.FindProperty(field));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ComicPanelBehaviour BehaviourOf(Playable playable) => ((ScriptPlayable<ComicPanelBehaviour>)playable).GetBehaviour();

        private static ComicPanelClip NewClip(Owned owned) => owned.Track(ScriptableObject.CreateInstance<ComicPanelClip>());

        // ---- ComicPanelClip ------------------------------------------------------------------------

        // Constructor 0x79E2B0 stores only m_scaleMultiplier 1.0f (0x3f800000).
        [Test]
        public void ClipConstructionDefaultsTheScaleMultiplierToOne()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<VisualQualityManager_SDT>();
                var serialized = new SerializedObject(NewClip(owned));
                Assert.That(serialized.FindProperty("m_scaleMultiplier").floatValue, Is.EqualTo(1f));
                Assert.That(serialized.FindProperty("m_screenPosition").vector2Value, Is.EqualTo(Vector2.zero));
            });
        }

        // CreatePlayable 0x79DE90..0x79DF9B: a ScriptPlayable of the behaviour whose template, texture, position,
        // rotation and animations are copied, and whose Vector2 scale is the multiplier on both axes (0x79df49..0x79df50
        // copy 0x3c to 0x34 and 0x38). With no VisualQualityManager the texture is left alone (0x79e0b9).
        [Test]
        public void CreatePlayableCopiesTheAuthoredFieldsAndTheScaleMultiplierOnBothAxes()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<VisualQualityManager_SDT>();
                ComicPanelClip clip = NewClip(owned);
                var texture = owned.Track(new RenderTexture(8, 8, 0));
                var template = owned.Host("Lucid comic panel template", false).AddComponent<ComicPanel>();
                var enter = owned.Track(new AnimationClip { name = "Lucid enter" });
                var loop = owned.Track(new AnimationClip { name = "Lucid loop" });
                var exit = owned.Track(new AnimationClip { name = "Lucid exit" });
                SetSerialized(clip, "m_comicPanelTemplate", property => property.objectReferenceValue = template);
                SetSerialized(clip, "m_renderTexture", property => property.objectReferenceValue = texture);
                SetSerialized(clip, "m_screenPosition", property => property.vector2Value = new Vector2(3f, 4f));
                SetSerialized(clip, "m_rotation", property => property.vector3Value = new Vector3(10f, 20f, 30f));
                SetSerialized(clip, "m_scaleMultiplier", property => property.floatValue = 2.5f);
                SetSerialized(clip, "m_enterAnim", property => property.objectReferenceValue = enter);
                SetSerialized(clip, "m_loopAnim", property => property.objectReferenceValue = loop);
                SetSerialized(clip, "m_exitAnim", property => property.objectReferenceValue = exit);
                WithGraph(owned, graph =>
                {
                    ComicPanelBehaviour behaviour = BehaviourOf(clip.CreatePlayable(graph, null));
                    Assert.That(behaviour.PanelTemplate, Is.SameAs(template));
                    Assert.That(behaviour.RenderTexture, Is.SameAs(texture));
                    Assert.That(behaviour.ScreenPosition, Is.EqualTo(new Vector2(3f, 4f)));
                    Assert.That(behaviour.Rotation, Is.EqualTo(new Vector3(10f, 20f, 30f)));
                    Assert.That(behaviour.Scale, Is.EqualTo(new Vector2(2.5f, 2.5f)));
                    Assert.That(behaviour.EnterAnim, Is.SameAs(enter));
                    Assert.That(behaviour.LoopAnim, Is.SameAs(loop));
                    Assert.That(behaviour.ExitAnim, Is.SameAs(exit));
                    Assert.That((texture.width, texture.height), Is.EqualTo((8, 8)), "no manager, no resize");
                });
            });
        }

        // InitialiseRenderTexture 0x79E020 (called from 0x79dff7 while m_initialised is clear): the texture takes
        // the manager's CutsceneRenderSize (+0xc0) truncated to integers (cvttss2si), and only then is the clip
        // marked initialised (0x79e114), so a later size is ignored.
        [Test]
        public void TheTextureTakesTheTruncatedCutsceneSizeOnce()
        {
            Owned.Run(owned =>
            {
                ComicPanelClip clip = NewClip(owned);
                var texture = owned.Track(new RenderTexture(8, 8, 0));
                owned.LeaseRow<SaveManager>();
                owned.LeaseRow<ScalablePerformance>();
                SetSerialized(clip, "m_renderTexture", property => property.objectReferenceValue = texture);
                // Added last, so the clip is destroyed (and its OnDisable runs) while the manager is still there.
                var manager = owned.Register(owned.Host("Lucid visual quality manager", false).AddComponent<VisualQualityManager_SDT>());
                manager.SetCutsceneRenderSize(new Vector2(64.9f, 32.5f));
                WithGraph(owned, graph =>
                {
                    clip.CreatePlayable(graph, null);
                    Assert.That((texture.width, texture.height), Is.EqualTo((64, 32)));
                    manager.SetCutsceneRenderSize(new Vector2(100f, 50f));
                    clip.CreatePlayable(graph, null);
                    Assert.That((texture.width, texture.height), Is.EqualTo((64, 32)), "already initialised");
                });
            });
        }

        // OnDisable 0x79E130: with a manager the texture is resized to its DefaultRenderSize (+0xc8, 1280x720),
        // but m_initialised is not touched, so the next playable still applies the cutscene size.
        [Test]
        public void OnDisableRestoresTheDefaultSizeWithoutMarkingTheClipInitialised()
        {
            Owned.Run(owned =>
            {
                ComicPanelClip clip = NewClip(owned);
                var texture = owned.Track(new RenderTexture(8, 8, 0));
                owned.LeaseRow<SaveManager>();
                owned.LeaseRow<ScalablePerformance>();
                SetSerialized(clip, "m_renderTexture", property => property.objectReferenceValue = texture);
                var manager = owned.Register(owned.Host("Lucid visual quality manager", false).AddComponent<VisualQualityManager_SDT>());
                manager.SetCutsceneRenderSize(new Vector2(64f, 32f));
                WithGraph(owned, graph =>
                {
                    Call(clip, "OnDisable");
                    Assert.That((texture.width, texture.height), Is.EqualTo((1280, 720)));
                    clip.CreatePlayable(graph, null);
                    Assert.That((texture.width, texture.height), Is.EqualTo((64, 32)), "OnDisable did not mark the clip initialised");
                    Call(clip, "OnDisable");
                    Assert.That((texture.width, texture.height), Is.EqualTo((1280, 720)));
                    clip.CreatePlayable(graph, null);
                    Assert.That((texture.width, texture.height), Is.EqualTo((1280, 720)), "now initialised, so not resized again");
                });
            });
        }

        // ---- ComicPanelTrack -----------------------------------------------------------------------

        // The class attribute 0x02000430: TrackClipType(typeof(ComicPanelClip)).
        [Test]
        public void TheTrackAcceptsComicPanelClips()
        {
            var attribute = (TrackClipTypeAttribute)Attribute.GetCustomAttribute(typeof(ComicPanelTrack), typeof(TrackClipTypeAttribute));
            Assert.That(attribute.inspectedType, Is.EqualTo(typeof(ComicPanelClip)));
        }

        // CreateTrackMixer 0x79EA50: a ScriptPlayable<ComicPanelMixer> with the requested input count, whose unique
        // handle is the track's own ToString (virtual slot 3, 0x79eb0d) - the engine's "name (type)" - so two
        // tracks keep distinct panel dictionaries.
        [Test]
        public void TheTrackMixerIsNamedAfterTheTrack()
        {
            Owned.Run(owned =>
            {
                var first = owned.Track(ScriptableObject.CreateInstance<ComicPanelTrack>());
                first.name = "Lucid comic track one";
                var second = owned.Track(ScriptableObject.CreateInstance<ComicPanelTrack>());
                second.name = "Lucid comic track two";
                WithGraph(owned, graph =>
                {
                    Playable mixer = first.CreateTrackMixer(graph, null, 3);
                    Assert.That(mixer.GetInputCount(), Is.EqualTo(3));
                    string handle = Get<string>(((ScriptPlayable<ComicPanelMixer>)mixer).GetBehaviour(), "m_thisTrackHandle");
                    Assert.That(handle, Is.EqualTo(first.ToString()));
                    Assert.That(handle, Does.StartWith("Lucid comic track one"));
                    Assert.That(handle, Does.Contain("ComicPanelTrack"));
                    string other = Get<string>(((ScriptPlayable<ComicPanelMixer>)second.CreateTrackMixer(graph, null, 0)).GetBehaviour(), "m_thisTrackHandle");
                    Assert.That(other, Is.Not.EqualTo(handle));
                });
            });
        }

        // ---- ComicPanelMixer -----------------------------------------------------------------------

        // SetUniqueTrackHandle 0x79E2D0 stores the name; OnGraphStart 0x79E2F0 calls the base, stores the playable's
        // input count (0x79e380) and, outside play mode, goes no further (0x79e39f), so no CutsceneManager is read.
        [Test]
        public void OnGraphStartCountsTheInputsAndRegistersNothingOutsidePlayMode()
        {
            Owned.Run(owned => WithGraph(owned, graph =>
            {
                ScriptPlayable<ComicPanelMixer> playable = ScriptPlayable<ComicPanelMixer>.Create(graph, 3);
                ComicPanelMixer mixer = playable.GetBehaviour();
                mixer.SetUniqueTrackHandle("Lucid handle");
                Assert.That(Get<string>(mixer, "m_thisTrackHandle"), Is.EqualTo("Lucid handle"));
                Assert.That(Get<int>(mixer, "m_inputCount"), Is.EqualTo(0));
                mixer.OnGraphStart(playable);
                Assert.That(Get<int>(mixer, "m_inputCount"), Is.EqualTo(3));
                Assert.That(Get<object>(mixer, "m_cutsceneManager"), Is.Null);
                Assert.That(Get<object>(mixer, "m_comicCutscene"), Is.Null);
            }));
        }

        // ProcessFrame 0x79E590, for one input whose weight is changed between frames: a zero weight with no
        // panel does nothing; a positive weight spawns the panel (only that, 0x79e690); an active panel below one
        // drives the enter animation (EnterExit), at one it starts looping (SetLooping), and below one again it
        // exits (the exit animation at 1 - weight); a zero weight despawns it (DespawnPanel).
        [Test]
        public void ProcessFrameSpawnsDrivesAndDespawnsThePanelByTheInputWeight()
        {
            Owned.Run(owned =>
            {
                // The animated clips each move the panel's animating root along one axis over one second.
                AnimationClip Moving(string name, string property)
                {
                    var moving = owned.Track(new AnimationClip { name = name, legacy = true });
                    moving.SetCurve("", typeof(Transform), property, AnimationCurve.Linear(0f, 0f, 1f, 1f));
                    return moving;
                }
                AnimationClip enter = Moving("Lucid enter", "localPosition.x");
                AnimationClip exit = Moving("Lucid exit", "localPosition.y");

                GameObject templateHost = owned.Host("Lucid comic panel template", true);
                GameObject animatingRoot = owned.Host("Lucid animating root", true, templateHost.transform);
                Animation animation = animatingRoot.AddComponent<Animation>();
                ComicPanel template = templateHost.AddComponent<ComicPanel>();
                SetSerialized(template, "m_animatingRoot", property => property.objectReferenceValue = animation);
                Transform container = owned.Host("Lucid comic panel container", true).transform;

                ComicCutscene cutscene = owned.Host("Lucid comic cutscene", false).AddComponent<ComicCutscene>();
                SetSerialized(cutscene, "m_panelContainerTransform", property => property.objectReferenceValue = container);
                SetSerialized(cutscene, "m_comicCamera", property => property.objectReferenceValue = owned.Host("Lucid comic camera", false).AddComponent<Camera>());

                ComicPanelClip clip = NewClip(owned);
                SetSerialized(clip, "m_comicPanelTemplate", property => property.objectReferenceValue = template);
                SetSerialized(clip, "m_screenPosition", property => property.vector2Value = new Vector2(3f, 4f));
                SetSerialized(clip, "m_scaleMultiplier", property => property.floatValue = 2.5f);
                SetSerialized(clip, "m_enterAnim", property => property.objectReferenceValue = enter);
                SetSerialized(clip, "m_exitAnim", property => property.objectReferenceValue = exit);
                owned.LeaseRow<VisualQualityManager_SDT>();

                WithGraph(owned, graph =>
                {
                    ScriptPlayable<ComicPanelMixer> mixerPlayable = ScriptPlayable<ComicPanelMixer>.Create(graph, 1);
                    Playable clipPlayable = clip.CreatePlayable(graph, null);
                    Assert.That(graph.Connect(clipPlayable, 0, mixerPlayable, 0), Is.True);
                    ComicPanelMixer mixer = mixerPlayable.GetBehaviour();
                    mixer.SetUniqueTrackHandle("Lucid comic track");
                    mixer.OnGraphStart(mixerPlayable);
                    // Reflection: OnGraphStart assigns the cutscene only while playing, which Edit Mode never does.
                    Set(mixer, "m_comicCutscene", cutscene);

                    void Frame(float weight)
                    {
                        mixerPlayable.SetInputWeight(0, weight);
                        mixer.ProcessFrame(mixerPlayable, default, null);
                    }
                    Transform Panel() => container.GetChild(0);
                    Transform Root() => Panel().Find("Lucid animating root");

                    Frame(0f);
                    Assert.That(container.childCount, Is.EqualTo(0), "a zero weight spawns nothing");

                    Frame(0.5f);
                    Assert.That(container.childCount, Is.EqualTo(1));
                    Assert.That(cutscene.PanelIsActive("Lucid comic track", 0), Is.True);
                    Assert.That(Panel().localPosition, Is.EqualTo(new Vector3(3f, 4f, 0f)));
                    Assert.That(Panel().localScale, Is.EqualTo(new Vector3(2.5f, 2.5f, 0f)), "the Vector2 scale keeps z = 0");
                    Assert.That(Root().gameObject.activeSelf, Is.False, "spawning only initialises the panel");

                    Frame(0.5f);
                    Assert.That(container.childCount, Is.EqualTo(1));
                    Assert.That(Root().gameObject.activeSelf, Is.True);
                    Assert.That(Root().localPosition.x, Is.EqualTo(0.5f).Within(1e-4f), "entering samples the enter animation at the weight");

                    Frame(1f);
                    Assert.That(Root().localPosition.x, Is.EqualTo(0.5f).Within(1e-4f), "looping samples nothing");

                    Frame(0.25f);
                    Assert.That(Root().localPosition.y, Is.EqualTo(0.75f).Within(1e-4f), "exiting samples the exit animation at 1 - weight");

                    Frame(0f);
                    Assert.That(container.childCount, Is.EqualTo(0));
                    Assert.That(cutscene.PanelIsActive("Lucid comic track", 0), Is.False);
                    Frame(0f);
                    Assert.That(container.childCount, Is.EqualTo(0), "a second zero weight finds nothing to despawn");
                });
            });
        }
    }
}
