using System;
using System.Collections;
using System.Collections.Generic;
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
    // (they need the control mapping's real button events) and TimeMachineSkipBehaviour (needs a CutsceneManager).
    public sealed class TimeMachineMixerPlayModeTests
    {
        private sealed class Scene
        {
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
    }
}
