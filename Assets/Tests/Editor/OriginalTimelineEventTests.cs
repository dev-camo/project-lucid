using System;
using System.Reflection;
using NUnit.Framework;
using ProjectLucid.Tests.WorldTriggersShared;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime TimelineEventBehaviour (TypeDef 0x02000004, x86-64 0x5060E0..0x50677F, with its closure
    // 0x02000005), TimelineEventClip (0x02000006, 0x506870..0x50698F) and TimelineEventTrack (0x02000008,
    // 0x5069A0..0x506EDF). The behaviour is a plain object whose serialized fields are the clip's authored data; the
    // target is a real component on an owned inactive host (the runtime EventInvocationTargetProbe). Edit Mode is
    // not playing, so the clip's edit-mode flag decides whether handlers resolve.
    public sealed class OriginalTimelineEventTests
    {
        private const string ProbeType = "ProjectLucid.Tests.WorldTriggersShared.EventInvocationTargetProbe";

        private static void WithEvent(string method, Action<Owned, TimelineEventBehaviour, EventInvocationTargetProbe> body,
            bool single = false, string argument = null, bool editMode = true) => Owned.Run(owned =>
        {
            Assert.That(Application.isPlaying, Is.False, "these cases run in Edit Mode");
            GameObject host = owned.Host("Lucid owned timeline event target", false);
            var probe = host.AddComponent<EventInvocationTargetProbe>();
            var behaviour = new TimelineEventBehaviour { TargetObject = host };
            Configure(behaviour, ProbeType + "." + method, single, argument, editMode);
            body(owned, behaviour, probe);
        });

        // The fields the timeline serializes into the clip.
        private static void Configure(TimelineEventBehaviour behaviour, string key, bool single, string argument, bool editMode)
        {
            Set(behaviour, "m_handlerKey", key);
            Set(behaviour, "m_expectsSingleParameter", single);
            Set(behaviour, "m_argumentValue", argument);
            Set(behaviour, "m_invokeEventInEditMode", editMode);
        }

        private static FrameData Frame(ulong frameId, float deltaTime)
        {
            object frame = default(FrameData);
            FieldInfo id = typeof(FrameData).GetField("m_FrameID", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo delta = typeof(FrameData).GetField("m_DeltaTime", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(id, Is.Not.Null, "FrameData keeps its frame id in m_FrameID");
            Assert.That(delta, Is.Not.Null, "FrameData keeps its delta time in m_DeltaTime");
            id.SetValue(frame, frameId);
            delta.SetValue(frame, Convert.ChangeType(deltaTime, delta.FieldType));
            return (FrameData)frame;
        }

        private static void Play(TimelineEventBehaviour behaviour, ulong frameId = 0, float deltaTime = 0f) =>
            behaviour.OnBehaviourPlay(default(Playable), Frame(frameId, deltaTime));

        // 0x5060e0 -> UpdateDelegates 0x5061b0 -> GetInvocationInfo 0x506240 / GetBehaviourAndMethod 0x5064a0: the key
        // "Type.Method" names a component of the target by Type.ToString() and one of its public instance methods.
        [Test]
        public void PlayInvokesTheNamedMethodOnTheTargetsComponent()
        {
            WithEvent("NoArguments", (owned, behaviour, probe) =>
            {
                Play(behaviour);
                Assert.That(probe.Calls, Is.EqualTo(new[] { "NoArguments" }));
            });
        }

        // 0x50629a: the lookup requires a void method with exactly the expected parameter count (b__0 0x506770);
        // EventInvocationInfo.Invoke(isSingleArg, value) (0x505870) converts the text to the parameter.
        [Test]
        public void ASingleParameterMethodReceivesTheConvertedArgumentValue()
        {
            WithEvent("TakeInt", (owned, behaviour, probe) =>
            {
                Play(behaviour);
                Assert.That(probe.Calls, Is.EqualTo(new[] { "TakeInt" }));
                Assert.That(probe.Arguments, Is.EqualTo(new object[] { 7 }));
            }, single: true, argument: "7");
        }

        [Test]
        public void AMethodWithTheWrongParameterCountIsNeverCalled()
        {
            WithEvent("TakeInt", (owned, behaviour, probe) =>
            {
                Play(behaviour);
                Assert.That(probe.Calls, Is.Empty, "no argument is expected, but the method takes one");
            }, single: false, argument: "7");
            WithEvent("NoArguments", (owned, behaviour, probe) =>
            {
                Play(behaviour);
                Assert.That(probe.Calls, Is.Empty, "an argument is expected, but the method takes none");
            }, single: true, argument: "7");
        }

        // 0x5060f3..0x506155: handlers resolve in play mode or when the clip asks for edit mode; otherwise the
        // lookup is skipped and no information is kept.
        [Test]
        public void NothingResolvesInEditModeUnlessTheClipAsksForIt()
        {
            WithEvent("NoArguments", (owned, behaviour, probe) =>
            {
                Play(behaviour);
                Assert.That(probe.Calls, Is.Empty);
                Assert.That(Get<object>(behaviour, "m_invocationInfo"), Is.Null);
            }, editMode: false);
        }

        // 0x5060f3..0x50610d: the delta time only matters after the first frame (a zero frame id always plays); a
        // zero or negative step does not.
        [Test]
        public void LaterFramesPlayOnlyWhenTimeAdvances()
        {
            WithEvent("NoArguments", (owned, behaviour, probe) =>
            {
                Play(behaviour, frameId: 5, deltaTime: 0f);
                Play(behaviour, frameId: 5, deltaTime: -0.5f);
                Assert.That(probe.Calls, Is.Empty);
                Play(behaviour, frameId: 5, deltaTime: 0.25f);
                Assert.That(probe.Calls.Count, Is.EqualTo(1));
                Play(behaviour, frameId: 0, deltaTime: 0f);
                Assert.That(probe.Calls.Count, Is.EqualTo(2), "the first frame plays without a time step");
                Play(behaviour, frameId: 5, deltaTime: float.NaN);
                Assert.That(probe.Calls.Count, Is.EqualTo(2), "a NaN step is not above zero");
            });
        }

        // 0x506256: the cached information is returned while its key is unchanged, not empty and not "none".
        [Test]
        public void TheResolvedInformationIsKeptWhileTheKeyIsUnchanged()
        {
            WithEvent("NoArguments", (owned, behaviour, probe) =>
            {
                Play(behaviour);
                object first = Get<object>(behaviour, "m_invocationInfo");
                Play(behaviour);
                Assert.That(Get<object>(behaviour, "m_invocationInfo"), Is.SameAs(first));
                Assert.That(probe.Calls.Count, Is.EqualTo(2));

                Set(behaviour, "m_handlerKey", ProbeType + ".TakeString");
                Set(behaviour, "m_expectsSingleParameter", true);
                Set(behaviour, "m_argumentValue", "hello");
                Play(behaviour);
                Assert.That(Get<object>(behaviour, "m_invocationInfo"), Is.Not.SameAs(first));
                Assert.That(probe.Calls, Is.EqualTo(new[] { "NoArguments", "NoArguments", "TakeString" }));
                Assert.That(probe.Arguments[2], Is.EqualTo("hello"));
            });
        }

        // 0x50631f..0x50633a / 0x50651a: a key that lower-cases to "none" (or is empty) resolves nothing, drops the
        // cached information and does not fault.
        [Test]
        public void ANoneKeyResolvesNothingAndDropsTheCachedInformation()
        {
            WithEvent("NoArguments", (owned, behaviour, probe) =>
            {
                Play(behaviour);
                Set(behaviour, "m_handlerKey", "NoNe");
                Assert.DoesNotThrow(() => Play(behaviour));
                Assert.That(Get<object>(behaviour, "m_invocationInfo"), Is.Null);
                Set(behaviour, "m_handlerKey", "");
                Assert.DoesNotThrow(() => Play(behaviour));
                Assert.That(probe.Calls.Count, Is.EqualTo(1));
            });
        }

        // 0x5064a0: an unparsable or unresolvable key faults with the original messages.
        [Test]
        public void UnresolvableKeysFaultWithTheirOwnMessages()
        {
            WithEvent("NoArguments", (owned, behaviour, probe) =>
            {
                Set(behaviour, "m_handlerKey", "NoSeparator");
                Assert.Throws<ArgumentOutOfRangeException>(() => Play(behaviour), "Substring(0, -1)");
                Set(behaviour, "m_handlerKey", ".Method");
                Assert.That(Assert.Throws<Exception>(() => Play(behaviour)).Message, Is.EqualTo("Unable to parse callback method: .Method"));
                Set(behaviour, "m_handlerKey", "Type.");
                Assert.That(Assert.Throws<Exception>(() => Play(behaviour)).Message, Is.EqualTo("Unable to parse callback method: Type."));
                Set(behaviour, "m_handlerKey", "Missing.Type.Method");
                Assert.That(Assert.Throws<Exception>(() => Play(behaviour)).Message,
                    Is.EqualTo("Unable to find target behaviour: key Missing.Type.Method typename Missing.Type"));
                behaviour.TargetObject = null;
                Assert.That(Assert.Throws<Exception>(() => Play(behaviour)).Message, Is.EqualTo("No target set for key Missing.Type.Method"));
            });
        }

        // 0x506930 allocates the behaviour before the PlayableAsset constructor; 0x506880 returns ClipCaps.None;
        // 0x506890 creates a script playable of that behaviour in the graph.
        [Test]
        public void TheClipHoldsAFreshBehaviourAndCreatesItsPlayable()
        {
            Owned.Run(owned =>
            {
                var clip = owned.Track(ScriptableObject.CreateInstance<TimelineEventClip>());
                Assert.That(clip.Behaviour, Is.Not.Null);
                Assert.That(ScriptableObject.CreateInstance<TimelineEventClip>().Behaviour, Is.Not.SameAs(clip.Behaviour));
                Assert.That(clip.clipCaps, Is.EqualTo(ClipCaps.None));
                PlayableGraph graph = PlayableGraph.Create("Lucid timeline event clip");
                try
                {
                    Playable playable = clip.CreatePlayable(graph, null);
                    Assert.That(playable.IsValid(), Is.True);
                    Assert.That(playable.GetPlayableType(), Is.EqualTo(typeof(TimelineEventBehaviour)));
                }
                finally { graph.Destroy(); }
            });
        }

        // 0x5069a0: every event clip receives the GameObject bound to the track (as-cast to GameObject) and the
        // mixer playable is a TimelineEventMixerBehaviour. Without a binding the target stays unset.
        [Test]
        public void TheTrackHandsItsBoundObjectToEveryEventClip()
        {
            Owned.Run(owned =>
            {
                var timeline = owned.Track(ScriptableObject.CreateInstance<TimelineAsset>());
                var track = owned.Track(timeline.CreateTrack<TimelineEventTrack>(null, "Lucid events"));
                TimelineClip timelineClip = track.CreateClip<TimelineEventClip>();
                var eventClip = (TimelineEventClip)owned.Track(timelineClip.asset);
                GameObject bound = owned.Host("Lucid owned bound object", false);
                GameObject directorHost = owned.Host("Lucid owned timeline director", false);
                var director = directorHost.AddComponent<PlayableDirector>();
                director.playableAsset = timeline;
                director.SetGenericBinding(track, bound);
                PlayableGraph graph = PlayableGraph.Create("Lucid timeline event track");
                try
                {
                    Playable mixer = track.CreateTrackMixer(graph, directorHost, 1);
                    Assert.That(mixer.GetPlayableType(), Is.EqualTo(typeof(TimelineEventMixerBehaviour)));
                    Assert.That(eventClip.Behaviour.TargetObject, Is.SameAs(bound));

                    eventClip.Behaviour.TargetObject = null;
                    director.SetGenericBinding(track, null);
                    track.CreateTrackMixer(graph, directorHost, 1);
                    Assert.That(eventClip.Behaviour.TargetObject == null, Is.True, "no bound object, no target");

                    // 0x506aa7: a binding that is not a GameObject converts to null instead of throwing.
                    director.SetGenericBinding(track, bound.transform);
                    Assert.DoesNotThrow(() => track.CreateTrackMixer(graph, directorHost, 1));
                    Assert.That(eventClip.Behaviour.TargetObject == null, Is.True, "a Transform binding is not a GameObject");
                }
                finally { graph.Destroy(); }
            });
        }
    }
}
