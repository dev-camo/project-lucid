using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime TimeMachineMixerBehaviour.ProcessFrame (TypeDef 0x02000809, x86-64 0x5FC650..0x5FC96F),
    // TimeMachineTrack.CreateTrackMixer (0x5FCD50) and TimeMachineClip.CreatePlayable (0x5FC510) through a real
    // PlayableDirector playing a real TimelineAsset. The mixer acts only while Application.isPlaying, takes the
    // director from the graph's resolver (0x5fc8a4) and runs each clip's action once. Clips are authored through
    // JsonUtility, which fills the serialized behaviour as the inspector would. Tolerances allow for the frames the
    // director advances between the action and the assertion. Not covered: the Input and InputHeld conditions
    // (they need the control mapping's real button events; the guard order is covered by hand-built graphs below) and
    // TimeMachineSkipBehaviour (needs a CutsceneManager). Playing a graph runs OnGraphStart (0x5FC3D0) on each clip, which
    // subscribes to ControlMapping and is never undone by the original; every director test therefore restores the
    // process-wide tables it touched (ControlTablesLease).
    public sealed class TimeMachineMixerPlayModeTests
    {
        // Snapshot of the ControlMapping callback tables OnGraphStart subscribes into (button up, axis end, swipe and
        // scroll wheel). Release removes keys added since and puts each surviving list back; the PlayMode assembly
        // cannot use the Editor-side StaticDictionaryLease.
        private sealed class ControlTablesLease
        {
            private static readonly string[] Tables = { "s_onButtonsUp", "s_onAxisEnd", "s_onSwipe", "s_onScrollWheel" };
            private readonly List<(IDictionary Table, Dictionary<object, (object Value, List<Action<float>> Callbacks)> Entries)> m_tables =
                new List<(IDictionary, Dictionary<object, (object, List<Action<float>>)>)>();

            public ControlTablesLease()
            {
                foreach (string name in Tables)
                {
                    var table = (IDictionary)typeof(ControlMapping).GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
                    var entries = new Dictionary<object, (object, List<Action<float>>)>();
                    foreach (DictionaryEntry entry in table)
                        entries[entry.Key] = (entry.Value, new List<Action<float>>(((FastAction<float>)entry.Value).GetInvocationList()));
                    m_tables.Add((table, entries));
                }
            }

            public void Release()
            {
                foreach ((IDictionary table, Dictionary<object, (object Value, List<Action<float>> Callbacks)> entries) in m_tables)
                {
                    var added = new List<object>();
                    foreach (object key in table.Keys) if (!entries.ContainsKey(key)) added.Add(key);
                    foreach (object key in added) table.Remove(key);
                    foreach (KeyValuePair<object, (object Value, List<Action<float>> Callbacks)> entry in entries)
                    {
                        table[entry.Key] = entry.Value.Value;
                        List<Action<float>> live = ((FastAction<float>)entry.Value.Value).GetInvocationList();
                        live.Clear();
                        live.AddRange(entry.Value.Callbacks);
                    }
                    if (table.Count != entries.Count) throw new InvalidOperationException("ControlMapping table not restored");
                }
            }
        }

        private sealed class Scene
        {
            public readonly ControlTablesLease Tables = new ControlTablesLease();
            public readonly List<Object> Assets = new List<Object>();
            public GameObject Host;
            public PlayableDirector Director;
            public TimelineAsset Timeline;
            public TimeMachineTrack Track;
        }

        private static Scene NewScene()
        {
            var scene = new Scene();
            scene.Timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            scene.Assets.Add(scene.Timeline);
            scene.Track = scene.Timeline.CreateTrack<TimeMachineTrack>(null, "Lucid time machine track");
            scene.Assets.Add(scene.Track);
            scene.Host = new GameObject("Lucid time machine director");
            scene.Director = scene.Host.AddComponent<PlayableDirector>();
            scene.Director.playOnAwake = false;
            scene.Director.extrapolationMode = DirectorWrapMode.Hold;
            scene.Director.playableAsset = scene.Timeline;
            return scene;
        }

        // A clip on the track at the given start and length whose behaviour is filled from JSON.
        private static TimelineClip AddClip(Scene scene, double start, double duration, string behaviourJson)
        {
            TimelineClip clip = scene.Track.CreateClip<TimeMachineClip>();
            scene.Assets.Add(clip.asset);
            clip.start = start;
            clip.duration = duration;
            JsonUtility.FromJsonOverwrite("{\"m_timeMachinePlayableAsset\":" + behaviourJson + "}", clip.asset);
            return clip;
        }

        private static void Dispose(Scene scene)
        {
            if (scene.Host != null) Object.Destroy(scene.Host);
            foreach (Object asset in scene.Assets) if (asset != null) Object.DestroyImmediate(asset);
            scene.Tables.Release();
        }

        private const int ActionJumpToTime = 1;
        private const int ActionJumpToMarker = 2;
        private const int ActionJumpToEnd = 3;
        private const int ActionPause = 4;

        // ProcessFrame 0x5FC650 runs the Pause action (PlayableDirector.Pause 0x5fc945) when the clip is active and its
        // condition is met, and marks the clip executed (0x5fc8e8) so the next frames do not pause again.
        [UnityTest]
        public IEnumerator APauseClipPausesTheDirectorOnlyOnce()
        {
            Scene scene = NewScene();
            try
            {
                AddClip(scene, 0.0, 100.0, "{\"m_action\":" + ActionPause + ",\"m_condition\":0}");
                scene.Director.Play();
                for (int frame = 0; frame < 3; frame++) yield return null;
                Assert.That(scene.Director.state, Is.EqualTo(PlayState.Paused), "the clip paused the director");
                scene.Director.Play();
                for (int frame = 0; frame < 3; frame++) yield return null;
                Assert.That(scene.Director.state, Is.EqualTo(PlayState.Playing), "the executed clip does not pause again");
            }
            finally { Dispose(scene); }
        }

        // The JumpToTime action (0x5fc90c) sets PlayableDirector.time to the authored time once; afterwards the
        // director advances normally from there.
        [UnityTest]
        public IEnumerator AJumpToTimeClipMovesTheDirectorToTheTimeOnce()
        {
            Scene scene = NewScene();
            try
            {
                AddClip(scene, 0.0, 100.0, "{\"m_action\":" + ActionJumpToTime + ",\"m_timeToJumpTo\":50,\"m_condition\":0}");
                scene.Director.Play();
                for (int frame = 0; frame < 3; frame++) yield return null;
                double jumped = scene.Director.time;
                Assert.That(jumped, Is.GreaterThanOrEqualTo(50.0).And.LessThan(60.0), "moved to the authored time");
                for (int frame = 0; frame < 3; frame++) yield return null;
                Assert.That(scene.Director.time, Is.GreaterThan(jumped).And.LessThan(60.0), "no second jump");
            }
            finally { Dispose(scene); }
        }

        // The JumpToMarker action (0x5fc91b) looks the marker label up in the track's MarkerClips (registered by
        // CreateTrackMixer from the Marker clip) and sets the director time to that clip's start.
        [UnityTest]
        public IEnumerator AJumpToMarkerClipMovesTheDirectorToTheMarkerClipsStart()
        {
            Scene scene = NewScene();
            try
            {
                AddClip(scene, 0.0, 10.0, "{\"m_action\":" + ActionJumpToMarker + ",\"m_markerToJumpTo\":\"target\",\"m_condition\":0}");
                AddClip(scene, 60.0, 10.0, "{\"m_action\":0,\"m_markerLabel\":\"target\",\"m_condition\":0}");
                scene.Director.Play();
                for (int frame = 0; frame < 3; frame++) yield return null;
                Assert.That(scene.Director.time, Is.GreaterThanOrEqualTo(60.0).And.LessThan(70.0));
            }
            finally { Dispose(scene); }
        }

        // The JumpToEnd action (0x5fc758) looks the clip's own label up and sets the time to that clip's end minus the
        // authored soften time (5 here): a 20 second clip is left at 15, well short of its end at 20. A later clip keeps
        // the 100 second timeline running past 20, so a jump to the plain end would land outside the asserted window.
        [UnityTest]
        public IEnumerator AJumpToEndClipLandsJustBeforeItsOwnEnd()
        {
            Scene scene = NewScene();
            try
            {
                AddClip(scene, 0.0, 20.0, "{\"m_action\":" + ActionJumpToEnd + ",\"m_markerLabel\":\"end\",\"m_endSoftenTime\":5,\"m_condition\":0}");
                AddClip(scene, 20.0, 80.0, "{\"m_action\":5,\"m_condition\":0}");
                scene.Director.Play();
                for (int frame = 0; frame < 3; frame++) yield return null;
                Assert.That(scene.Director.time, Is.GreaterThanOrEqualTo(15.0).And.LessThan(18.0));
            }
            finally { Dispose(scene); }
        }

        // An undefined condition value is never met (ConditionMet default branch), so the clip never executes.
        [UnityTest]
        public IEnumerator AConditionThatIsNeverMetLeavesTheDirectorPlayingUntouched()
        {
            Scene scene = NewScene();
            try
            {
                AddClip(scene, 0.0, 100.0, "{\"m_action\":" + ActionPause + ",\"m_condition\":3}");
                scene.Director.Play();
                for (int frame = 0; frame < 5; frame++) yield return null;
                Assert.That(scene.Director.state, Is.EqualTo(PlayState.Playing));
                Assert.That(scene.Director.time, Is.GreaterThan(0.0));
            }
            finally { Dispose(scene); }
        }

        // Hand-built graph for the ProcessFrame guard order. Inputs are real TimeMachineBehaviour playables cloned from a
        // TimeMachineClip template (the mixer casts each input to exactly that type), the mixer is called directly while
        // Application.isPlaying, and the pending press is raised through the private InputPressed callback because the
        // control mapping's button events cannot be driven here.
        private sealed class ManualGraph
        {
            public PlayableGraph Graph;
            public ScriptPlayable<TimeMachineMixerBehaviour> Mixer;
            public readonly List<ScriptPlayable<TimeMachineBehaviour>> Inputs = new List<ScriptPlayable<TimeMachineBehaviour>>();
            public readonly List<Object> Assets = new List<Object>();

            public ManualGraph(params (string Json, float Weight)[] clips)
            {
                Graph = PlayableGraph.Create("Lucid time machine guard order");
                Mixer = ScriptPlayable<TimeMachineMixerBehaviour>.Create(Graph, clips.Length);
                for (int i = 0; i < clips.Length; i++)
                {
                    var clip = ScriptableObject.CreateInstance<TimeMachineClip>();
                    Assets.Add(clip);
                    JsonUtility.FromJsonOverwrite("{\"m_timeMachinePlayableAsset\":" + clips[i].Json + "}", clip);
                    var input = (ScriptPlayable<TimeMachineBehaviour>)clip.CreatePlayable(Graph, null);
                    Inputs.Add(input);
                    Graph.Connect(input, 0, Mixer, i);
                    Mixer.SetInputWeight(i, clips[i].Weight);
                }
            }

            public void Press(int input) =>
                typeof(TimeMachineBehaviour).GetMethod("InputPressed", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(Inputs[input].GetBehaviour(), new object[] { 1f });

            public bool PressPending(int input) =>
                (bool)typeof(TimeMachineBehaviour).GetField("m_inputPressOccurred", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(Inputs[input].GetBehaviour());

            public bool Executed(int input) => Inputs[input].GetBehaviour().ClipExecuted;

            public void Process() => Mixer.GetBehaviour().ProcessFrame(Mixer, new FrameData(), null);

            public void Dispose()
            {
                if (Graph.IsValid()) Graph.Destroy();
                foreach (Object asset in Assets) if (asset != null) Object.DestroyImmediate(asset);
            }
        }

        private static string InputPause(int condition = 1) => "{\"m_action\":" + ActionPause + ",\"m_condition\":" + condition + "}";

        // A started director to act on: the timeline holds one DoNothing clip, so it plays for the whole case.
        private static PlayableDirector PlayingDirector(Scene scene)
        {
            AddClip(scene, 0.0, 100.0, "{\"m_action\":5,\"m_condition\":0}");
            scene.Director.Play();
            return scene.Director;
        }

        // The weight test comes first (ucomiss weight, 0 / jbe 0x5fc839 skips zero, negative and NaN weights), then the
        // executed test (0x5fc842), then ConditionMet, which consumes the pending press (0x5fc84c..0x5fc87d), then the
        // director lookup (isinst 0x5fc8a4..0x5fc8e2). So a press survives a skipped clip and is consumed by a clip that
        // then finds no director, and that clip is not marked executed.
        [Test]
        public void AnInputClipConsumesThePressBeforeTheDirectorLookupAndStaysUnexecutedWithoutOne()
        {
            var graph = new ManualGraph((InputPause(), 1f));
            try
            {
                graph.Press(0);
                Assert.That(graph.PressPending(0), Is.True, "precondition");
                graph.Process();
                Assert.That(graph.PressPending(0), Is.False, "ConditionMet ran before the director test");
                Assert.That(graph.Executed(0), Is.False, "no director, no execution");
            }
            finally { graph.Dispose(); }
        }

        [Test]
        public void ClipsWithZeroOrNaNWeightOrAlreadyExecutedDoNotTouchThePendingPress()
        {
            foreach (float weight in new[] { 0f, float.NaN, -1f })
            {
                var graph = new ManualGraph((InputPause(), weight));
                try
                {
                    graph.Press(0);
                    graph.Process();
                    Assert.That(graph.PressPending(0), Is.True, "weight " + weight + " skips the clip before ConditionMet");
                    Assert.That(graph.Executed(0), Is.False);
                }
                finally { graph.Dispose(); }
            }
            var executed = new ManualGraph((InputPause(), 1f));
            try
            {
                executed.Inputs[0].GetBehaviour().ClipExecuted = true;
                executed.Press(0);
                executed.Process();
                Assert.That(executed.PressPending(0), Is.True, "an executed clip is skipped before ConditionMet");
            }
            finally { executed.Dispose(); }
        }

        // Every ready clip in input order acts, so the later JumpToTime (60) overrides the earlier one (30).
        [UnityTest]
        public IEnumerator LaterClipsInTheSameFrameOverrideEarlierTimeChanges()
        {
            Scene scene = NewScene();
            ManualGraph graph = null;
            try
            {
                PlayableDirector director = PlayingDirector(scene);
                yield return null;
                graph = new ManualGraph(
                    ("{\"m_action\":" + ActionJumpToTime + ",\"m_timeToJumpTo\":30,\"m_condition\":0}", 1f),
                    ("{\"m_action\":" + ActionJumpToTime + ",\"m_timeToJumpTo\":60,\"m_condition\":0}", 1f));
                graph.Graph.SetResolver(director);
                graph.Process();
                Assert.That(director.time, Is.EqualTo(60.0).Within(1e-3));
                Assert.That(graph.Executed(0) && graph.Executed(1), Is.True, "both clips executed");
            }
            finally
            {
                graph?.Dispose();
                Dispose(scene);
            }
        }

        // With a director the pending press is consumed, the pause action runs and the clip is marked executed.
        [UnityTest]
        public IEnumerator AnInputClipWithADirectorPausesItAndIsMarkedExecuted()
        {
            Scene scene = NewScene();
            ManualGraph graph = null;
            try
            {
                PlayableDirector director = PlayingDirector(scene);
                yield return null;
                Assert.That(director.state, Is.EqualTo(PlayState.Playing), "precondition");
                graph = new ManualGraph((InputPause(), 1f));
                graph.Graph.SetResolver(director);
                graph.Process();
                Assert.That(director.state, Is.EqualTo(PlayState.Playing), "no press yet, so nothing ran");
                Assert.That(graph.Executed(0), Is.False);
                graph.Press(0);
                graph.Process();
                Assert.That(graph.PressPending(0), Is.False);
                Assert.That(director.state, Is.EqualTo(PlayState.Paused));
                Assert.That(graph.Executed(0), Is.True);
            }
            finally
            {
                graph?.Dispose();
                Dispose(scene);
            }
        }
    }
}
