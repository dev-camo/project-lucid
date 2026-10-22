using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Apple.Arcade.SplashScreen;
using Apple.Arcade.SplashScreen.Interfaces;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using ProjectLucid.Offline;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using UnityEngine.Video;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Apple.Arcade.SplashScreen.dll (TypeDefs 0x02000004..0x02000008; x86-64 0x425580..0x4262e0). The
    // declarations are the DummyDll rows (flags, order, attributes); the behaviour is the shipped x86-64
    // bodies named in each comment. Hook H-056 (the splash skip in SplashScreenPlayer.Start) is covered here
    // with the original completion path it takes.
    public sealed class OriginalAppleArcadeSplashScreenTests
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                         BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        // Runs body on a player on an inactive owned host, with the frame rate put back afterwards.
        // The inactive host never runs Awake or Start; the cases call what they exercise.
        private static void WithPlayer(Action<SplashScreenPlayer, VideoPlayer, int[]> body) =>
            Owned.Run(owned =>
            {
                int priorFrameRate = Application.targetFrameRate;
                VerificationFixtureOwnership.RunThenCleanUp(() =>
                {
                    GameObject host = owned.Host("Lucid apple splash", false);
                    var player = host.AddComponent<SplashScreenPlayer>();
                    VideoPlayer video = host.GetComponent<VideoPlayer>();
                    Assert.That(video, Is.Not.Null, "RequireComponent adds the video player");
                    var fired = new int[1];
                    var loopEvent = new UnityEvent();
                    loopEvent.AddListener(() => fired[0]++);
                    Set(player, "m_onPlayerLoopPointReached", loopEvent);
                    Set(player, "_videoPlayer", video);
                    body(player, video, fired);
                },
                () => Application.targetFrameRate = priorFrameRate);
            });

        // dump.cs TypeDefIndex 14556 / 14555 with the DummyDll rows.
        [Test]
        public void SplashScreenPlayerKeepsItsOriginalDeclarations()
        {
            Type player = typeof(SplashScreenPlayer);
            Assert.That(player.FullName, Is.EqualTo("Apple.Arcade.SplashScreen.SplashScreenPlayer"));
            Assert.That(player.Assembly.GetName().Name, Is.EqualTo("Apple.Arcade.SplashScreen"));
            Assert.That(player.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            AssertMembers(player, 0x00100001,
                new (string, int)[]
                {
                    ("_videoPlayer", 0x0001),
                    ("_audioSource", 0x0001),
                    ("m_targetFPS", 0x0001),
                    ("m_onPlayerLoopPointReached", 0x0001),
                    ("m_cachedTargetFrameRate", 0x0001),
                },
                new (string, int)[]
                {
                    ("Start", 0x0081),
                    ("OnVideoPlayerReady", 0x0081),
                    ("Action_OnVideoCompleted", 0x0086),
                    ("OnLoopPointReached", 0x0081),
                    ("OnVideoCompleted", 0x0081),
                    (".ctor", 0x1886),
                });
            Assert.That(player.GetCustomAttributesData().Select(data => data.AttributeType).ToArray(),
                Is.EqualTo(new[] { typeof(RequireComponent) }));
            Assert.That(player.GetCustomAttribute<RequireComponent>().m_Type0, Is.EqualTo(typeof(VideoPlayer)));
            foreach (string serialized in new[] { "_videoPlayer", "_audioSource", "m_targetFPS", "m_onPlayerLoopPointReached" })
                Assert.That(Field(player, serialized).GetCustomAttribute<SerializeField>(), Is.Not.Null, serialized);
            Assert.That(Field(player, "m_cachedTargetFrameRate").GetCustomAttributes(typeof(SerializeField), false), Is.Empty);

            Type machine = player.GetNestedType("<Start>d__5", BindingFlags.NonPublic);
            Assert.That(machine, Is.Not.Null, "the async Start state machine keeps its generated name");
            Assert.That(machine.IsValueType, Is.True);
            Assert.That(machine.GetInterfaces(), Is.EqualTo(new[] { typeof(IAsyncStateMachine) }));
            Assert.That(machine.GetFields(Any).OrderBy(field => field.MetadataToken).Select(field => field.Name).ToArray(),
                Is.EqualTo(new[] { "<>1__state", "<>t__builder", "<>4__this", "<>7__wrap1", "<>u__1" }));
            Assert.That(machine.GetField("<>t__builder", Any).FieldType, Is.EqualTo(typeof(AsyncVoidMethodBuilder)));
            Assert.That(machine.GetField("<>7__wrap1", Any).FieldType, Is.EqualTo(typeof(VideoPlayer)));
            Assert.That(machine.GetField("<>u__1", Any).FieldType, Is.EqualTo(typeof(TaskAwaiter<VideoClip>)));
            Assert.That(player.GetMethod("Start", Any).GetCustomAttribute<AsyncStateMachineAttribute>().StateMachineType, Is.EqualTo(machine));
            Assert.That(player.GetNestedTypes(Any).Select(type => type.Name).ToArray(), Is.EqualTo(new[] { "<Start>d__5" }));
        }

        // dump.cs TypeDefIndex 14554 / 14553 / 14557 with the DummyDll rows.
        [Test]
        public void TheVideoProviderAndItsInterfaceKeepTheirOriginalDeclarations()
        {
            Type provider = typeof(ResourcesSplashScreenVideoProvider);
            Assert.That(provider.FullName, Is.EqualTo("Apple.Arcade.SplashScreen.ResourcesSplashScreenVideoProvider"));
            Assert.That(provider.GetInterfaces(), Is.EqualTo(new[] { typeof(ISplashScreenVideoProvider) }));
            AssertMembers(provider, 0x00100001,
                new (string, int)[0],
                new (string, int)[]
                {
                    ("GetSplashScreenVideo", 0x01e6),
                    ("GetScreenAspect", 0x0081),
                    ("GetOrientationRepresentation", 0x0081),
                    ("GetAspectRatioRepresentation", 0x0081),
                    ("IsLowEndDevice", 0x0081),
                    (".ctor", 0x1886),
                });

            Type closure = provider.GetNestedType("<>c__DisplayClass0_0", BindingFlags.NonPublic);
            Assert.That(closure, Is.Not.Null);
            Assert.That(closure.GetFields(Any).OrderBy(field => field.MetadataToken).Select(field => (field.Name, field.FieldType)).ToArray(),
                Is.EqualTo(new[]
                {
                    ("tcs", typeof(TaskCompletionSource<VideoClip>)),
                    ("request", typeof(ResourceRequest)),
                }));
            MethodInfo callback = closure.GetMethod("<GetSplashScreenVideo>b__0", Any);
            Assert.That((int)callback.Attributes, Is.EqualTo(0x0083));
            Assert.That(callback.GetParameters().Select(parameter => parameter.ParameterType).ToArray(), Is.EqualTo(new[] { typeof(AsyncOperation) }));

            Type contract = typeof(ISplashScreenVideoProvider);
            Assert.That(contract.FullName, Is.EqualTo("Apple.Arcade.SplashScreen.Interfaces.ISplashScreenVideoProvider"));
            Assert.That((int)contract.Attributes, Is.EqualTo(0x000000a1));
            MethodInfo only = contract.GetMethods(Any).Single();
            Assert.That(only.Name, Is.EqualTo("GetSplashScreenVideo"));
            Assert.That((int)only.Attributes, Is.EqualTo(0x05c6));
            Assert.That(only.ReturnType, Is.EqualTo(typeof(Task<VideoClip>)));
        }

        // GetAspectRatioRepresentation 0x06000006 (x86-64 0x425a60) converts the float to double and compares it
        // with the doubles at 0x29a6bc0: 1.87, 1.77, 1.6, each as "greater or equal". The float constants 1.77f
        // (1.7699999809) is below the double 1.77, while 1.87f (1.8700000048) and 1.6f (1.6000000238) are above
        // theirs: a float comparison, or a strict one, would move these.
        [TestCase(2.3f, "19.5x9", TestName = "AspectTextOfAWideScreenIsNineteenPointFiveByNine")]
        [TestCase(1.87f, "19.5x9", TestName = "AspectTextAtTheFloatNearest187IsNineteenPointFiveByNine")]
        [TestCase(1.86f, "16x9", TestName = "AspectTextJustBelow187IsSixteenByNine")]
        [TestCase(1.78f, "16x9", TestName = "AspectTextOfSixteenByNineIsSixteenByNine")]
        [TestCase(1.77f, "16x10", TestName = "AspectTextAtTheFloatNearest177FallsBelowTheDoubleThreshold")]
        [TestCase(1.6f, "16x10", TestName = "AspectTextAtTheFloatNearest16IsSixteenByTen")]
        [TestCase(1.59f, "4x3", TestName = "AspectTextJustBelow16IsFourByThree")]
        [TestCase(1f, "4x3", TestName = "AspectTextOfASquareScreenIsFourByThree")]
        public void AspectTextComparesTheFloatAgainstTheOriginalDoubleThresholds(float aspect, string expected)
        {
            Assert.That(Call(new ResourcesSplashScreenVideoProvider(), "GetAspectRatioRepresentation", aspect), Is.EqualTo(expected));
        }

        // GetOrientationRepresentation 0x06000005 (0x425b20) reads the screen size, discards it and returns
        // "Landscape"; IsLowEndDevice 0x06000007 (0x425b60) is a constant false; GetScreenAspect 0x06000004
        // (0x425a10) is the long side over the short side as float division.
        [Test]
        public void ProviderHelpersKeepTheirNativeResults()
        {
            var provider = new ResourcesSplashScreenVideoProvider();
            Assert.That(Call(provider, "GetOrientationRepresentation"), Is.EqualTo("Landscape"));
            Assert.That(Call(provider, "IsLowEndDevice"), Is.False);
            int width = Screen.width, height = Screen.height;
            float expected = width < height ? (float)height / width : (float)width / height;
            Assert.That((float)Call(provider, "GetScreenAspect"), Is.EqualTo(expected));
            Assert.That(expected, Is.GreaterThanOrEqualTo(1f));
        }

        // GetSplashScreenVideo 0x06000003 (0x425580) asks Resources for "<aspect>/<aspect>_Landscape" and its
        // completion callback 0x0600000a (0x425b80) sets the task result from the request asset. This project
        // the request asset, whatever it found: the clip when the project ships one at that path, null when it
        // does not (never a fault). The expectation is the same Resources lookup made directly, so the case
        // holds with and without the recovered splash videos in the project.
        [UnityTest]
        public IEnumerator TheTaskCompletesWithWhateverTheResourcesRequestFound()
        {
            var lookup = new ResourcesSplashScreenVideoProvider();
            string aspect = (string)Call(lookup, "GetAspectRatioRepresentation", Call(lookup, "GetScreenAspect"));
            VideoClip expected = Resources.Load<VideoClip>(aspect + "/" + aspect + "_Landscape");
            Task<VideoClip> task = new ResourcesSplashScreenVideoProvider().GetSplashScreenVideo();
            var timer = Stopwatch.StartNew();
            while (!task.IsCompleted && timer.Elapsed.TotalSeconds < 10) yield return null;
            Assert.That(task.IsCompleted, Is.True, "the Resources request finished");
            Assert.That(task.IsFaulted, Is.False);
            Assert.That(task.Result, expected == null ? (IResolveConstraint)Is.Null : Is.SameAs(expected));
        }

        // Action_OnVideoCompleted 0x0600000d (0x425d60), OnLoopPointReached 0x0600000e (0x425e40) and
        // OnVideoCompleted 0x0600000f (0x425dd0) are three separate identical bodies: restore the cached frame
        // rate, then raise the loop-point event if there is one (the null test at +0x38).
        [TestCase("Action_OnVideoCompleted", 0, TestName = "ActionOnVideoCompletedRestoresTheFrameRateThenRaisesTheEvent")]
        [TestCase("OnLoopPointReached", 1, TestName = "LoopPointReachedRestoresTheFrameRateThenRaisesTheEvent")]
        [TestCase("OnVideoCompleted", 0, TestName = "VideoCompletedRestoresTheFrameRateThenRaisesTheEvent")]
        public void EachCompletionPathRestoresTheFrameRateThenRaisesTheEvent(string method, int parameters)
        {
            WithPlayer((player, video, fired) =>
            {
                Set(player, "m_cachedTargetFrameRate", 77);
                Application.targetFrameRate = 30;
                Call(player, method, parameters == 0 ? new object[0] : new object[] { video });
                Assert.That(Application.targetFrameRate, Is.EqualTo(77));
                Assert.That(fired[0], Is.EqualTo(1));

                Set(player, "m_onPlayerLoopPointReached", null);
                Application.targetFrameRate = 30;
                Assert.DoesNotThrow(() => Call(player, method, parameters == 0 ? new object[0] : new object[] { video }));
                Assert.That(Application.targetFrameRate, Is.EqualTo(77), "the frame rate is restored even without an event");
            });
        }

        // OnVideoPlayerReady 0x0600000c (0x425cb0) removes its handler from the source before it touches the
        // serialized video player, so a null source faults first and leaves frame-ready events switched on.
        [Test]
        public void ReadyHandlerFaultsOnANullSourceBeforeSwitchingFrameEventsOff()
        {
            WithPlayer((player, video, fired) =>
            {
                video.sendFrameReadyEvents = true;
                Assert.Throws<NullReferenceException>(() => Call(player, "OnVideoPlayerReady", null, 0L));
                Assert.That(video.sendFrameReadyEvents, Is.True);
                Assert.That(fired[0], Is.EqualTo(0));
            });
        }

        // The constructor 0x06000010 (0x425eb0) stores the 60 FPS default before the MonoBehaviour constructor.
        [Test]
        public void TheDefaultTargetFrameRateIsSixty()
        {
            WithPlayer((player, video, fired) => Assert.That(Get<int>(player, "m_targetFPS"), Is.EqualTo(60)));
        }

        // Hook H-056. Start 0x0600000b (0x425c10) caches the frame rate, loads the video and plays it; the
        // event fires only when the video ends. Offline on a desktop target Start caches the frame rate and
        // runs OnVideoCompleted at once: the frame rate is restored to the cached value (not zeroed), the
        // scene's persistent listener is raised exactly once, and no clip is loaded or played.
        [Test]
        public void StartSkipsTheSplashThroughTheOriginalCompletionPathOnADesktopTarget()
        {
            Assert.That(OfflineDesktopPlatform.IsCurrent, Is.True, "the verification editor is a desktop target");
            WithPlayer((player, video, fired) =>
            {
                Application.targetFrameRate = 144;
                Call(player, "Start");
                Assert.That(fired[0], Is.EqualTo(1));
                Assert.That(Get<int>(player, "m_cachedTargetFrameRate"), Is.EqualTo(144));
                Assert.That(Application.targetFrameRate, Is.EqualTo(144));
                Assert.That(video.clip, Is.Null);
                Assert.That(video.isPlaying, Is.False);
                Assert.That(video.sendFrameReadyEvents, Is.False);
                LogAssert.NoUnexpectedReceived();
            });
        }

        [Test]
        public void OnlyDesktopTargetsSkipTheSplash()
        {
            foreach (RuntimePlatform platform in new[]
            {
                RuntimePlatform.OSXPlayer, RuntimePlatform.WindowsPlayer, RuntimePlatform.LinuxPlayer,
                RuntimePlatform.OSXEditor, RuntimePlatform.WindowsEditor, RuntimePlatform.LinuxEditor,
            })
                Assert.That(OfflineDesktopPlatform.IsDesktop(platform), Is.True, platform.ToString());
            foreach (RuntimePlatform platform in new[]
            {
                RuntimePlatform.IPhonePlayer, RuntimePlatform.tvOS, RuntimePlatform.Android, RuntimePlatform.Switch,
            })
                Assert.That(OfflineDesktopPlatform.IsDesktop(platform), Is.False, platform.ToString());
        }

        // The original Start stays compiled behind the guard: the hook reads both switches and takes the
        // completion path first, and the original frame-rate cache, provider call, clip, loop-point
        // subscription, frame-ready switch, play, pause and ready subscription follow in the original order.
        [Test]
        public void StartKeepsTheOriginalBodyAfterTheGuard()
        {
            Type machine = typeof(SplashScreenPlayer).GetNestedType("<Start>d__5", BindingFlags.NonPublic);
            MethodInfo moveNext = machine.GetMethod("MoveNext", Any);
            string[] names = OriginalILCallSequence.Names(OriginalILCallSequence.Calls(moveNext));
            string[] expected =
            {
                "OfflineAdapters.get_Active", "OfflineDesktopPlatform.get_IsCurrent", "Application.get_targetFrameRate",
                "SplashScreenPlayer.OnVideoCompleted",
                "Application.get_targetFrameRate", "ResourcesSplashScreenVideoProvider..ctor",
                "ResourcesSplashScreenVideoProvider.GetSplashScreenVideo", "VideoPlayer.set_clip",
                "Application.set_targetFrameRate", "VideoPlayer.add_loopPointReached", "VideoPlayer.set_sendFrameReadyEvents",
                "VideoPlayer.Play", "VideoPlayer.Pause", "VideoPlayer.add_frameReady",
            };
            int at = -1;
            foreach (string name in expected)
            {
                int next = Array.IndexOf(names, name, at + 1);
                Assert.That(next, Is.GreaterThan(at), name + " follows the previous call in IL order: " + string.Join(", ", names));
                at = next;
            }
        }
    }
}
