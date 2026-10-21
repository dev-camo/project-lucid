using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Video;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime TimelineHooks (TypeDef 0x02000a35, x86-64 0x6B8190..0x6B82DF), VideoPlayerWrapper
    // (0x02000a40, 0x6B97E0..0x6B99CF) and SequencedTimeEvent (0x02000629, 0x56AC20..0x56AC9F). Components are real,
    // on owned inactive hosts that never wake. The director and video player subscriptions made in Start and
    // removed in OnDestroy, and SequencedTimeEvent's trigger (it reads the runtime time configuration and
    // starts a coroutine), are not covered.
    public sealed class OriginalTimelineHookTests
    {
        [Test]
        public void EmissionCheck_HookWrapperAndSequencedEventKeepTheirFlagsFieldsAndMethods()
        {
            AssertMembers(typeof(TimelineHooks), 0x00100101,
                new[] { ("m_director", 0x0001), ("m_started", 0x0001), ("m_stopped", 0x0001) },
                new[] { ("Start", 0x0081), ("OnDestroy", 0x0081), ("Action_Started", 0x0086), ("OnDirectorStopped", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(TimelineHooks), 1, 2);
            AssertMembers(typeof(VideoPlayerWrapper), 0x00100001,
                new[] { ("m_videoPlayer", 0x0001), ("m_onVideoStarted", 0x0001), ("m_onVideoCompleted", 0x0001) },
                new[] { ("Start", 0x0081), ("OnDestroy", 0x0081), ("OnVideoComplete", 0x0081), ("SkipVideo", 0x0081), ("Action_SkipVideo", 0x0086), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(VideoPlayerWrapper), 2, 1);
            AssertMembers(typeof(SequencedTimeEvent), 0x00100001,
                new[] { ("m_toSetting", 0x0001), ("m_timeScaling", 0x0001), ("m_timeManipulator", 0x0001), ("m_resetAfter", 0x0001) },
                new[] { ("OnEffectTriggered", 0x00c4), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(SequencedTimeEvent), 2, 1);
        }

        // 0x6b8290 / 0x6b82b0: each raises its event behind a plain null test.
        [Test]
        public void TheHooksRaiseTheirEventsOnlyWhenAssigned()
        {
            Owned.Run(owned =>
            {
                var hooks = owned.Host("Lucid owned timeline hooks", false).AddComponent<TimelineHooks>();
                int started = 0, stopped = 0;
                var startedEvent = new UnityEvent();
                startedEvent.AddListener(() => started++);
                var stoppedEvent = new UnityEvent();
                stoppedEvent.AddListener(() => stopped++);
                Set(hooks, "m_started", startedEvent); // serialized in the original
                Set(hooks, "m_stopped", stoppedEvent);

                hooks.Action_Started();
                Assert.That((started, stopped), Is.EqualTo((1, 0)));
                Call(hooks, "OnDirectorStopped", (PlayableDirector)null);
                Assert.That((started, stopped), Is.EqualTo((1, 1)), "the director argument is ignored");

                Set(hooks, "m_started", null);
                Set(hooks, "m_stopped", null);
                Assert.DoesNotThrow(hooks.Action_Started);
                Assert.DoesNotThrow(() => Call(hooks, "OnDirectorStopped", (PlayableDirector)null));
                Assert.That((started, stopped), Is.EqualTo((1, 1)));
            });
        }

        // 0x6b9900, 0x6b9940 and 0x6b9980 are one body: stop the player, then raise the completed event if assigned.
        [Test]
        public void EndingOrSkippingTheVideoStopsItAndRaisesTheCompletedEvent()
        {
            Owned.Run(owned =>
            {
                GameObject host = owned.Host("Lucid owned video wrapper", false);
                var player = host.AddComponent<VideoPlayer>();
                var wrapper = host.AddComponent<VideoPlayerWrapper>();
                int completed = 0;
                var completedEvent = new UnityEvent();
                completedEvent.AddListener(() => completed++);
                Set(wrapper, "m_videoPlayer", player); // serialized in the original
                Set(wrapper, "m_onVideoCompleted", completedEvent);

                wrapper.Action_SkipVideo();
                Assert.That(completed, Is.EqualTo(1));
                Call(wrapper, "OnVideoComplete", player);
                Assert.That(completed, Is.EqualTo(2));
                Call(wrapper, "SkipVideo");
                Assert.That(completed, Is.EqualTo(3));
                Assert.That(player.isPlaying, Is.False);

                Set(wrapper, "m_onVideoCompleted", null);
                Assert.DoesNotThrow(wrapper.Action_SkipVideo);
                Assert.That(completed, Is.EqualTo(3));
            });
        }
    }
}
