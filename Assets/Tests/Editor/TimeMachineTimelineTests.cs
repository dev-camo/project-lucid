using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using static ProjectLucid.Tests.TimelineTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime TimeMachineBehaviour (TypeDef 0x02000805, x86-64 0x5FC360..0x5FC4EF), TimeMachineClip
    // (0x02000808, 0x5FC4F0..0x5FC61F), TimeMachineMixerBehaviour (0x02000809, 0x5FC620..0x5FC97F),
    // TimeMachineSkipBehaviour (0x0200080a, 0x5FC980..0x5FCBBF), TimeMachineSkipClip (0x0200080b, 0x5FCBC0..0x5FCD4F)
    // and TimeMachineTrack (0x0200080c, 0x5FCD50..0x5FD43F). Assets and playables are real: tracks and clips come
    // from a TimelineAsset, behaviours from ScriptPlayable on a real PlayableGraph. The mixer's and the skip
    // behaviour's frame logic is gated on Application.isPlaying and is covered by the play-mode tests; here only the
    // Edit Mode guards are checked.
    //
    // Not covered (each needs a started CutsceneManager, which cannot be lifecycle-forced here); the expectations to
    // verify natively when a genuine CutsceneManager fixture exists:
    //  - TimeMachineSkipBehaviour.OnBehaviourPlay (0x5FCA86): the SkipEnabledState selects EnableCutsceneSkip (0x7A3500)
    //    or DisableCutsceneSkip (0x7A31C0).
    //  - TimeMachineSkipBehaviour.ProcessFrame (0x5FCB7D): condition 2 (InputHeld) stores ConsumeSkipHoldEvent (0x7A3700)
    //    into the hold flag (+0x3a), condition 1 (Input) stores ConsumeSkipTapEvent (0x7A3670) into the press flag (+0x39),
    //    any other condition touches neither; a version that swaps tap and hold would otherwise pass.
    public sealed class TimeMachineTimelineTests
    {
        private static readonly BindingFlags Statics = BindingFlags.Static | BindingFlags.NonPublic;

        // A behaviour whose protected configuration the case sets directly, as an authored asset would hold it.
        private sealed class ConfiguredBehaviour : TimeMachineBehaviour
        {
            public ConfiguredBehaviour Configure(TimeMachineCondition condition)
            {
                m_condition = condition;
                return this;
            }
            public ConfiguredBehaviour ConfigureInput(GameInput input)
            {
                m_conditionInput = input;
                return this;
            }
            public void ReleaseHeldInput() { m_inputHoldOccurred = true; }
            // The press handler is private and fed by ControlMapping; a subclass raises the same pending flag.
            public void PressInput() { m_inputPressOccurred = true; }
            public bool HoldOccurred => m_inputHoldOccurred;
            public bool PressOccurred => m_inputPressOccurred;
        }

        // The constructor 0x5FC4D0 stores 0.1 (0x3dcccccd) as the end soften time; every other field is zero or null.
        [Test]
        public void BehaviourDefaultsToAMarkerWithAnEndSoftenTimeOfOneTenth()
        {
            var behaviour = new TimeMachineBehaviour();
            Assert.That(behaviour.EndSoftenTime, Is.EqualTo(0.1f));
            Assert.That(behaviour.Action, Is.EqualTo(TimeMachineBehaviour.TimeMachineAction.Marker));
            Assert.That(behaviour.Condition, Is.EqualTo(TimeMachineBehaviour.TimeMachineCondition.Always));
            Assert.That(behaviour.MarkerLabel, Is.Null);
            Assert.That(behaviour.MarkerToJumpTo, Is.Null);
            Assert.That(behaviour.TimeToJumpTo, Is.EqualTo(0f));
            Assert.That(behaviour.ConditionInput, Is.EqualTo(default(GameInput)));
            Assert.That(behaviour.ClipExecuted, Is.False);
        }

        // ConditionMet 0x5FC490 switches on m_condition: Always is true; Input reports and clears the press flag
        // (+0x39); InputHeld reports and clears the hold flag (+0x3a); any other value is false and touches nothing.
        // InputPressed 0x5FC480 only sets the press flag.
        [Test]
        public void ConditionMetReportsAndConsumesTheMatchingFlag()
        {
            var always = new ConfiguredBehaviour().Configure(TimeMachineBehaviour.TimeMachineCondition.Always);
            Assert.That(always.ConditionMet(), Is.True);
            Assert.That(always.ConditionMet(), Is.True, "nothing is consumed");

            var press = new ConfiguredBehaviour().Configure(TimeMachineBehaviour.TimeMachineCondition.Input);
            Assert.That(press.ConditionMet(), Is.False, "no press yet");
            Call(press, "InputPressed", 1f);
            Assert.That(press.PressOccurred, Is.True);
            Assert.That(press.ConditionMet(), Is.True);
            Assert.That(press.PressOccurred, Is.False, "consumed");
            Assert.That(press.ConditionMet(), Is.False);
            press.ReleaseHeldInput();
            Assert.That(press.ConditionMet(), Is.False, "a hold does not satisfy a press");
            Assert.That(press.HoldOccurred, Is.True, "and is not consumed by it");

            var hold = new ConfiguredBehaviour().Configure(TimeMachineBehaviour.TimeMachineCondition.InputHeld);
            Call(hold, "InputPressed", 1f);
            Assert.That(hold.ConditionMet(), Is.False, "a press does not satisfy a hold");
            hold.ReleaseHeldInput();
            Assert.That(hold.ConditionMet(), Is.True);
            Assert.That(hold.HoldOccurred, Is.False, "consumed");
            Assert.That(hold.ConditionMet(), Is.False);
            Assert.That(hold.PressOccurred, Is.True, "the press flag was never touched");
        }

        [Test]
        public void AnUndefinedConditionIsNeverMetAndConsumesNothing()
        {
            var undefined = new ConfiguredBehaviour().Configure((TimeMachineBehaviour.TimeMachineCondition)3);
            Call(undefined, "InputPressed", 1f);
            undefined.ReleaseHeldInput();
            Assert.That(undefined.ConditionMet(), Is.False);
            Assert.That(undefined.PressOccurred, Is.True);
            Assert.That(undefined.HoldOccurred, Is.True);
        }

        // OnGraphStart 0x5FC3D0 subscribes the private InputPressed to ControlMapping for the condition input on
        // the Up trigger (edx = 2) with the default joystick (-1): the button-up, axis-end, swipe and scroll
        // tables each gain that callback. The four static tables are leased and put back.
        [Test]
        public void OnGraphStartSubscribesTheConditionInputForTheUpTrigger()
        {
            Owned.Run(owned =>
            {
                string[] tables = { "s_onButtonsUp", "s_onAxisEnd", "s_onSwipe", "s_onScrollWheel" };
                owned.Lease(new VerificationFixtureOwnership.StaticDictionaryLease(typeof(ControlMapping), tables));
                var behaviour = new ConfiguredBehaviour().ConfigureInput(GameInput.SkipTheSplash);
                foreach (string table in tables)
                    Assert.That(Subscribers(table), Is.Empty, table + " starts without a subscription to the input");
                behaviour.OnGraphStart(default(Playable));
                foreach (string table in tables)
                {
                    List<Action<float>> subscribers = Subscribers(table);
                    Assert.That(subscribers, Has.Count.EqualTo(1), table);
                    Assert.That(subscribers[0].Target, Is.SameAs(behaviour), table);
                    Assert.That(subscribers[0].Method.Name, Is.EqualTo("InputPressed"), table);
                }
                Subscribers("s_onButtonsUp")[0](1f);
                Assert.That(behaviour.PressOccurred, Is.True, "the subscribed callback records the press");
            });
        }

        private static List<Action<float>> Subscribers(string table)
        {
            var dictionary = (IDictionary)typeof(ControlMapping).GetField(table, Statics | BindingFlags.Public).GetValue(null);
            foreach (DictionaryEntry entry in dictionary)
            {
                object key = entry.Key;
                var input = (GameInput)key.GetType().GetField("GameInput").GetValue(key);
                var joystick = (int)key.GetType().GetField("JoystickIndex").GetValue(key);
                if (input == GameInput.SkipTheSplash && joystick == -1)
                    return ((FastAction<float>)entry.Value).GetInvocationList();
            }
            return new List<Action<float>>();
        }

        // The clip constructor 0x5FC5B0 creates the behaviour template (end soften time 0.1) before the base
        // constructor; clipCaps (0x5FC500) is zero; CreatePlayable 0x5FC510 clones the template into a
        // ScriptPlayable<TimeMachineBehaviour> with no inputs.
        [Test]
        public void ClipCreatesAClonedBehaviourPlayableFromItsAuthoredTemplate()
        {
            Owned.Run(owned =>
            {
                TimeMachineClip clip = owned.Track(ScriptableObject.CreateInstance<TimeMachineClip>());
                Assert.That(clip.Behaviour, Is.Not.Null);
                Assert.That(clip.Behaviour.EndSoftenTime, Is.EqualTo(0.1f));
                Assert.That(clip.clipCaps, Is.EqualTo(ClipCaps.None));
                Author(clip, serialized =>
                {
                    serialized.FindProperty("m_timeMachinePlayableAsset.m_action").intValue = (int)TimeMachineBehaviour.TimeMachineAction.JumpToTime;
                    serialized.FindProperty("m_timeMachinePlayableAsset.m_timeToJumpTo").floatValue = 4.5f;
                });
                Playable playable = clip.CreatePlayable(NewGraph(owned), null);
                Assert.That(playable.GetPlayableType(), Is.EqualTo(typeof(TimeMachineBehaviour)));
                Assert.That(playable.GetInputCount(), Is.EqualTo(0));
                TimeMachineBehaviour cloned = ((ScriptPlayable<TimeMachineBehaviour>)playable).GetBehaviour();
                Assert.That(cloned, Is.Not.SameAs(clip.Behaviour));
                Assert.That(cloned.Action, Is.EqualTo(TimeMachineBehaviour.TimeMachineAction.JumpToTime));
                Assert.That(cloned.TimeToJumpTo, Is.EqualTo(4.5f));
            });
        }

        // The skip behaviour constructor 0x5FC9A0 copies action, label, condition, input, marker, time and soften
        // time from the supplied behaviour (the 8-byte moves at +0x20 and +0x30 cover input and soften time) and
        // stores the skip state; ClipExecuted and the input flags stay at their defaults.
        [Test]
        public void SkipBehaviourCopiesTheAuthoredFieldsAndTheSkipState()
        {
            var source = new SourceBehaviour();
            source.Author();
            foreach (bool skip in new[] { true, false })
            {
                var copy = new TimeMachineSkipBehaviour(source, skip);
                Assert.That(copy.Action, Is.EqualTo(TimeMachineBehaviour.TimeMachineAction.JumpToMarker));
                Assert.That(copy.MarkerLabel, Is.EqualTo("label"));
                Assert.That(copy.Condition, Is.EqualTo(TimeMachineBehaviour.TimeMachineCondition.InputHeld));
                Assert.That(copy.ConditionInput, Is.EqualTo(GameInput.SkipTheSplash));
                Assert.That(copy.MarkerToJumpTo, Is.EqualTo("target"));
                Assert.That(copy.TimeToJumpTo, Is.EqualTo(3.5f));
                Assert.That(copy.EndSoftenTime, Is.EqualTo(0.25f));
                Assert.That(copy.SkipEnabledState, Is.EqualTo(skip));
                Assert.That(copy.ClipExecuted, Is.False, "the executed flag is not copied");
            }
            Assert.That(new TimeMachineSkipBehaviour().EndSoftenTime, Is.EqualTo(0.1f), "the parameterless constructor keeps the base default");
        }

        private sealed class SourceBehaviour : TimeMachineBehaviour
        {
            public void Author()
            {
                m_action = TimeMachineAction.JumpToMarker;
                m_markerLabel = "label";
                m_condition = TimeMachineCondition.InputHeld;
                m_conditionInput = GameInput.SkipTheSplash;
                m_markerToJumpTo = "target";
                m_timeToJumpTo = 3.5f;
                m_endSoftenTime = 0.25f;
                ClipExecuted = true;
            }
        }

        // TimeMachineSkipClip: the constructor 0x5FCCE0 enables skipping; CreatePlayable 0x5FCBC0 builds a
        // TimeMachineSkipBehaviour from the base clip's behaviour and the clip's own skip state.
        [Test]
        public void SkipClipCreatesASkipBehaviourPlayableFromItsAuthoredBehaviour()
        {
            Owned.Run(owned =>
            {
                TimeMachineSkipClip clip = owned.Track(ScriptableObject.CreateInstance<TimeMachineSkipClip>());
                Assert.That(Get<bool>(clip, "m_skipEnabledState"), Is.True);
                Author(clip, serialized =>
                {
                    serialized.FindProperty("m_skipEnabledState").boolValue = false;
                    serialized.FindProperty("m_timeMachinePlayableAsset.m_action").intValue = (int)TimeMachineBehaviour.TimeMachineAction.Pause;
                    serialized.FindProperty("m_timeMachinePlayableAsset.m_markerLabel").stringValue = "skip marker";
                });
                Playable playable = clip.CreatePlayable(NewGraph(owned), null);
                Assert.That(playable.GetPlayableType(), Is.EqualTo(typeof(TimeMachineSkipBehaviour)));
                TimeMachineSkipBehaviour behaviour = ((ScriptPlayable<TimeMachineSkipBehaviour>)playable).GetBehaviour();
                Assert.That(behaviour.SkipEnabledState, Is.False);
                Assert.That(behaviour.Action, Is.EqualTo(TimeMachineBehaviour.TimeMachineAction.Pause));
                Assert.That(behaviour.MarkerLabel, Is.EqualTo("skip marker"));
            });
        }

        // OnBehaviourPlay 0x5FCA10 and ProcessFrame 0x5FCB10 both test Application.isPlaying first (0x5fca3b, 0x5fcb37)
        // and return, so outside play mode they neither look up the cutscene manager nor read its events.
        [Test]
        public void SkipBehaviourDoesNothingOutsidePlayMode()
        {
            var behaviour = new TimeMachineSkipBehaviour { SkipEnabledState = true };
            Assert.DoesNotThrow(() => behaviour.OnBehaviourPlay(default(Playable), default(FrameData)));
            Assert.DoesNotThrow(() => behaviour.ProcessFrame(default(Playable), default(FrameData), null));
            Assert.That(Get<CutsceneManager>(behaviour, "m_cutsceneManager"), Is.Null);
        }

        // ProcessFrame 0x5FC650 returns at once outside play mode (Application.isPlaying 0x5fc712). Past that guard the
        // loop evaluates ConditionMet first (0x5fc850), which consumes an Input condition's pending press, so a press
        // that is still pending afterwards shows the guard returned before any input was examined.
        [Test]
        public void MixerDoesNothingOutsidePlayMode()
        {
            Owned.Run(owned =>
            {
                PlayableGraph graph = NewGraph(owned);
                ScriptPlayable<TimeMachineMixerBehaviour> mixer = ScriptPlayable<TimeMachineMixerBehaviour>.Create(graph, 1);
                ScriptPlayable<ConfiguredBehaviour> input = ScriptPlayable<ConfiguredBehaviour>.Create(graph, 0);
                graph.Connect(input, 0, mixer, 0);
                mixer.SetInputWeight(0, 1f);
                ConfiguredBehaviour pressed = input.GetBehaviour().Configure(TimeMachineBehaviour.TimeMachineCondition.Input);
                pressed.PressInput();
                TimeMachineMixerBehaviour behaviour = mixer.GetBehaviour();
                Assert.That(behaviour.MarkerClips, Is.Null);
                behaviour.MarkerClips = new Dictionary<string, TimelineClip>();
                Assert.DoesNotThrow(() => behaviour.ProcessFrame(mixer, default(FrameData), null));
                Assert.That(pressed.PressOccurred, Is.True, "the pending press is not consumed");
                Assert.That(pressed.ClipExecuted, Is.False);
            });
        }

        private static TimelineClip AddClip(TimeMachineTrack track, Owned owned, Action<SerializedObject> author)
        {
            TimelineClip clip = track.CreateClip<TimeMachineClip>();
            owned.Track(clip.asset);
            Author(clip.asset, author);
            return clip;
        }

        private static Action<SerializedObject> Behaviour(int action, string label = null, string marker = null, float time = 0f) => serialized =>
        {
            serialized.FindProperty("m_timeMachinePlayableAsset.m_action").intValue = action;
            if (label != null) serialized.FindProperty("m_timeMachinePlayableAsset.m_markerLabel").stringValue = label;
            if (marker != null) serialized.FindProperty("m_timeMachinePlayableAsset.m_markerToJumpTo").stringValue = marker;
            serialized.FindProperty("m_timeMachinePlayableAsset.m_timeToJumpTo").floatValue = time;
        };

        // CreateTrackMixer 0x5FCD50: a ScriptPlayable<TimeMachineMixerBehaviour> whose MarkerClips is a fresh dictionary,
        // and every clip is relabelled by its action (jump table at 0x5fd3cc): Marker "● label", JumpToTime "→ (t) time",
        // JumpToMarker "→ ● marker", JumpToEnd "⇥ label", Pause "||", DoNothing "-", with "<add label>" for a missing
        // text, and any other action leaves the name. Marker and JumpToEnd clips with a non-null label are
        // registered under it, the first one winning (0x5fd12b/0x5fd1c9 test only for null, so an empty label is
        // registered; Unity serialisation turns an unauthored null string into an empty one, so the null case is
        // not observable through assets).
        [Test]
        public void TrackLabelsEveryClipByItsActionAndRegistersTheMarkers()
        {
            Owned.Run(owned =>
            {
                var timeline = owned.Track(ScriptableObject.CreateInstance<TimelineAsset>());
                TimeMachineTrack track = owned.Track(timeline.CreateTrack<TimeMachineTrack>(null, "Lucid time machine track"));
                TimelineClip marker = AddClip(track, owned, Behaviour(0, label: "a"));
                // Writing "" over the null of a fresh in-memory behaviour changes nothing for the serialised object, so the
                // label is first given a text; the second write then stores an empty string, as an authored asset holds.
                TimelineClip empty = AddClip(track, owned, Behaviour(0, label: "x"));
                Author(empty.asset, Behaviour(0, label: ""));
                TimelineClip unlabelled = AddClip(track, owned, Behaviour(0));
                TimelineClip jumpToTime = AddClip(track, owned, Behaviour(1, time: 3f));
                TimelineClip jumpToMarker = AddClip(track, owned, Behaviour(2, marker: "m"));
                TimelineClip jumpToNowhere = AddClip(track, owned, Behaviour(2));
                TimelineClip jumpToEnd = AddClip(track, owned, Behaviour(3, label: "e"));
                TimelineClip pause = AddClip(track, owned, Behaviour(4));
                TimelineClip doNothing = AddClip(track, owned, Behaviour(5));
                TimelineClip duplicate = AddClip(track, owned, Behaviour(0, label: "a"));
                TimelineClip unlabelledEnd = AddClip(track, owned, Behaviour(3));
                TimelineClip undefined = AddClip(track, owned, Behaviour(9));

                Playable playable = track.CreateTrackMixer(NewGraph(owned), null, 12);

                Assert.That(playable.GetPlayableType(), Is.EqualTo(typeof(TimeMachineMixerBehaviour)));
                Assert.That(playable.GetInputCount(), Is.EqualTo(12));
                Assert.That(marker.displayName, Is.EqualTo("● a"));
                Assert.That(unlabelled.displayName, Is.EqualTo("● <add label>"));
                Assert.That(empty.displayName, Is.EqualTo("● <add label>"));
                Assert.That(jumpToTime.displayName, Is.EqualTo("→ (t) 3"));
                Assert.That(jumpToMarker.displayName, Is.EqualTo("→ ● m"));
                Assert.That(jumpToNowhere.displayName, Is.EqualTo("→ ● <add label>"));
                Assert.That(jumpToEnd.displayName, Is.EqualTo("⇥ e"));
                Assert.That(pause.displayName, Is.EqualTo("||"));
                Assert.That(doNothing.displayName, Is.EqualTo("-"));
                Assert.That(duplicate.displayName, Is.EqualTo("● a"));
                Assert.That(unlabelledEnd.displayName, Is.EqualTo("⇥ <add label>"));
                Assert.That(undefined.displayName, Is.EqualTo("TimeMachineClip"), "an undefined action keeps the name");

                Dictionary<string, TimelineClip> registered = ((ScriptPlayable<TimeMachineMixerBehaviour>)playable).GetBehaviour().MarkerClips;
                Assert.That(registered.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray(), Is.EqualTo(new[] { "", "a", "e" }));
                Assert.That(registered["a"], Is.SameAs(marker), "the first clip with a label wins");
                Assert.That(registered[""], Is.SameAs(empty), "an empty label is registered, once");
                Assert.That(registered["e"], Is.SameAs(jumpToEnd));
            });
        }

        // The track attributes in metadata order: TrackClipType(TimeMachineClip), TrackColor(0.7366781, 0.3261246,
        // 0.8529412), then the two Il2Cpp options (ArrayBoundsChecks, NullChecks).
        [Test]
        public void TrackKeepsItsClipTypeAndColour()
        {
            Assert.That(typeof(TimeMachineTrack).GetCustomAttributesData().Select(data => data.AttributeType.Name).ToArray(),
                Is.EqualTo(new[] { "TrackClipTypeAttribute", "TrackColorAttribute", "Il2CppSetOptionAttribute", "Il2CppSetOptionAttribute" }));
            Assert.That(typeof(TimeMachineTrack).GetCustomAttribute<TrackClipTypeAttribute>().inspectedType, Is.EqualTo(typeof(TimeMachineClip)));
            Color colour = typeof(TimeMachineTrack).GetCustomAttribute<TrackColorAttribute>().color;
            Assert.That(colour.r, Is.EqualTo(0.7366781f).Within(1e-7f));
            Assert.That(colour.g, Is.EqualTo(0.3261246f).Within(1e-7f));
            Assert.That(colour.b, Is.EqualTo(0.8529412f).Within(1e-7f));
        }
    }
}
