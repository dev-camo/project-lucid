using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Offline;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Offline hooks of the boot flow: H-055 (the FairPlay check state), H-056 (the Apple Arcade splash
    // player, covered in OriginalAppleArcadeSplashScreenTests), H-057 (cross promotion) and H-064 (the
    // Boot state arming the splash skip for an s_apple scene whose splash player is not bound). The H-064
    // cases run the adapter's parts on owned objects and a test graph; the Boot state itself is not entered
    // here, because its original body registers the save store and loads the scene through Addressables. Each case
    // runs the hooked original method with the adapters active and shows that the offline path
    // is taken and that no plugin, network or folder is touched. An unexpected error or exception
    // log fails a Unity test, so the original's DllNotFoundException log (a missing FPBundleWrapper)
    // and its "Coroutine couldn't be started" refusal (a download on an inactive host) would
    // surface here. The retained original bodies are run, with the adapters off, by the existing
    // cases for HLCrossPromo.Start; the FairPlay original is never invoked.
    public sealed class OfflineBootHookTests
    {
        private static string Name() => "Lucid-offline-boot-" + Guid.NewGuid().ToString("N");
        private static FiniteStateMachine Machine() => new FiniteStateMachine(Name(), skipAddToManager: true);
        private static FSMUser User() => new FSMUser(new FSMStorage(0));

        [Test]
        public void TheAdapterSwitchIsOnAndTheVerificationOverrideRestoresIt()
        {
            Assert.That(OfflineAdapters.Active, Is.True);
            using (OfflineAdapters.Override(false))
            {
                Assert.That(OfflineAdapters.Active, Is.False);
                using (OfflineAdapters.Override(true))
                    Assert.That(OfflineAdapters.Active, Is.True);
                Assert.That(OfflineAdapters.Active, Is.False, "the inner restore puts back the outer value");
            }
            Assert.That(OfflineAdapters.Active, Is.True);
            IDisposable twice = OfflineAdapters.Override(false);
            twice.Dispose();
            using (OfflineAdapters.Override(false))
            {
                twice.Dispose();
                Assert.That(OfflineAdapters.Active, Is.False, "a second dispose restores nothing");
            }
        }

        // ApplicationStateBootFairplay.DoOnEnter 0x06000644 (x86-64 0x53cb10) tail-calls
        // FairPlay.RegisterWithFairPlay and does nothing else, so the registration returning
        // is its whole success path. With the adapters active entering the state registers
        // nothing: the missing FPBundleWrapper library would otherwise log a
        // DllNotFoundException and request a quit.
        [Test]
        public void FairPlayCheckEntersThroughItsSuccessPathWithoutTheNativeLibrary()
        {
            IFSMState state = ApplicationStateBootFairplay.ConstructInstance(Machine(), Name(), string.Empty);
            state.OnEnter(User(), null);
            UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
        }

        // The original registration stays compiled behind the guard, after it and only after it.
        [Test]
        public void FairPlayCheckKeepsTheOriginalRegistrationAfterTheGuard()
        {
            MethodInfo enter = typeof(ApplicationStateBootFairplay).GetMethod("DoOnEnter",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            var calls = OriginalILCallSequence.Calls(enter);
            Assert.That(OriginalILCallSequence.Names(calls),
                Is.EqualTo(new[] { "OfflineAdapters.get_Active", "FairPlay.RegisterWithFairPlay" }));
            Assert.That(calls[0].Offset, Is.LessThan(calls[1].Offset));
        }

        // The no-content resource manager reports the metadata download as failed at once, holds no metadata
        // file, nothing pending and no texture, which is what the real manager reports for an unreachable
        // server with an empty cache (CrossPromoResourceManager 0x06000083, 0x06000084).
        [Test]
        public void TheNoContentResourceManagerReportsAFailedDownloadAndNoContent()
        {
            Owned.Run(owned =>
            {
                owned.Lease(new OwnedCrossPromoConfiguration("{\"m_canUseLocalStorage\":true}"));
                var row = owned.Lease(new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<CrossPromo>()));
                row.Register(null);
                var host = owned.Host("Lucid cross promo", false).AddComponent<HLCrossPromo>();
                var iterator = (IEnumerator)Call(host, "Start");
                Assert.That(iterator.MoveNext(), Is.False);
                row.AdoptCurrent();
                ICrossPromoResourceManager resources =
                    Get<ICrossPromoResourceManager>(ProcessManager.GetSystem<CrossPromo>(), "m_resourceManager");

                bool? outcome = null;
                int callbacks = 0;
                resources.DownloadMetaData(success => { outcome = success; callbacks++; });
                Assert.That(outcome, Is.False);
                Assert.That(callbacks, Is.EqualTo(1));
                Assert.That(resources.RequestMetaData(), Is.Null);
                Assert.That(resources.HasPendingDownloads, Is.False);
                Assert.That(resources.RequestIconTexture(default), Is.Null);
                Assert.That(resources.RequestFullScreenTexture(default), Is.Null);
                Assert.DoesNotThrow(() => resources.DownloadMetaData(null));
                Assert.DoesNotThrow(() => resources.VerifyAdResources(null));
                Assert.DoesNotThrow(() => resources.ReleaseIconTexture(null));
                Assert.DoesNotThrow(() => resources.ReleaseFullScreenTexture(null));
                Action<bool> handler = success => { };
                Assert.DoesNotThrow(() => { resources.DownloadCompleted += handler; resources.DownloadCompleted -= handler; });
            });
        }

        // The original start-up stays compiled behind the guard: in the iterator the guard is read first, then
        // the offline registration, and the original's configuration read and singleton waits follow.
        [Test]
        public void CrossPromoStartKeepsTheOriginalBodyAfterTheGuard()
        {
            Type iterator = typeof(HLCrossPromo).GetNestedType("<Start>d__0", BindingFlags.NonPublic);
            MethodInfo moveNext = iterator.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            string[] names = OriginalILCallSequence.Names(OriginalILCallSequence.Calls(moveNext));
            string[] expected =
            {
                "OfflineAdapters.get_Active", "OfflineCrossPromo.Register",
                "SystemConfiguration.GetConfig", "MonoSingleton`1.WaitOnInstance", "HLUnityCore.GetDeviceISO2CountryCode",
                "MonoSingleton`1.WaitOnInstance", "Language.GetLanguage", "CrossPromo..ctor",
            };
            int at = -1;
            foreach (string name in expected)
            {
                int next = Array.IndexOf(names, name, at + 1);
                Assert.That(next, Is.GreaterThan(at), name + " follows the previous call in IL order: " + string.Join(", ", names));
                at = next;
            }
        }

        // The Boot state arms the splash skip first, on desktop targets only, then runs its whole original body:
        // base enter, display setup, the save store, the level cache, then the scene load and its handle record.
        [Test]
        public void BootArmsTheSplashSkipThenKeepsTheOriginalBody()
        {
            MethodInfo enter = typeof(ApplicationStateBoot).GetMethod("DoOnEnter",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            string[] names = OriginalILCallSequence.Names(OriginalILCallSequence.Calls(enter));
            Assert.That(names.Take(4), Is.EqualTo(new[]
            {
                "OfflineAdapters.get_Active", "OfflineDesktopPlatform.get_IsCurrent", "OfflineAppleSplash.Arm", "FSMState.DoOnEnter",
            }), string.Join(", ", names));
            string[] expected =
            {
                "FSMState.DoOnEnter", "Application.get_platform", "HLPropertyStore.LoadFromIdentifier",
                "GameLevels.CacheLevels", "Addressables.LoadSceneAsync",
            };
            int at = -1;
            foreach (string name in expected)
            {
                int next = Array.IndexOf(names, name, at + 1);
                Assert.That(next, Is.GreaterThan(at), name + " follows the previous call in IL order: " + string.Join(", ", names));
                at = next;
            }
            Assert.That(names.Count(name => name == "OfflineAppleSplash.Arm"), Is.EqualTo(1));
        }

        // s_apple holds two UnloadSceneAfterFirstBoot components: the one beside the splash VideoPlayer (the
        // splash player's listener, Action_Unload(true)) and the EventSystem's (the controller skip,
        // Action_Unload(false)). The adapter picks the first whichever order the roots come in, and stands
        // down when a splash player component is present, since H-056 then runs the completion itself.
        [Test]
        public void TheSplashListenerIsTheUnloaderBesideTheVideoPlayer()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<HLPropertyStore>();
                GameObject events = owned.Host("Lucid apple event system", false);
                UnloadSceneAfterFirstBoot controllerSkip = events.AddComponent<UnloadSceneAfterFirstBoot>();
                GameObject splash = owned.Host("Lucid apple splash", false);
                splash.AddComponent<VideoPlayer>();
                UnloadSceneAfterFirstBoot listener = splash.AddComponent<UnloadSceneAfterFirstBoot>();

                Assert.That(OfflineAppleSplash.FindSplashListener(new[] { events, splash }), Is.SameAs(listener));
                Assert.That(OfflineAppleSplash.FindSplashListener(new[] { splash, events }), Is.SameAs(listener));
                Assert.That(OfflineAppleSplash.FindSplashListener(new[] { events }), Is.Null, "no video player, no listener");
                Assert.That(controllerSkip, Is.Not.Null);

                GameObject bound = owned.Host("Lucid apple bound splash", false);
                bound.AddComponent<Apple.Arcade.SplashScreen.SplashScreenPlayer>();
                Assert.That(OfflineAppleSplash.FindSplashListener(new[] { events, splash, bound }), Is.Null, "H-056 handles a bound scene");
                Assert.That(OfflineAppleSplash.FindSplashListener(new[] { bound, splash }), Is.Null);
            });
        }

        // The listener call waits while the graph is in Boot (unloading before Boot sees AppleLoaded would strand
        // it there), then is the splash player's own: Action_Unload(true), which records the forced unload and
        // waits on the save store's load. No store exists here, so the handler is only registered; it is removed
        // afterwards and nothing is unloaded.
        [Test]
        public void TheSplashListenerCallWaitsUntilTheGraphHasLeftBoot()
        {
            FieldInfo loadHandlers = Field(typeof(HLPropertyStore), "LoadHandlers");
            Assert.That(Field(typeof(HLPropertyStore), "s_internalInstance").GetValue(null), Is.Null, "no foreign store would run the unload");
            Assert.That(loadHandlers.GetValue(null), Is.Null, "no foreign load handler");
            Owned.Run(owned =>
            {
                owned.LeaseRow<HLPropertyStore>();
                VerificationFixtureOwnership.RunThenCleanUp(() =>
                {
                    GameObject splash = owned.Host("Lucid apple splash", false);
                    splash.AddComponent<VideoPlayer>();
                    UnloadSceneAfterFirstBoot listener = splash.AddComponent<UnloadSceneAfterFirstBoot>();
                    FiniteStateMachine graph = Machine();
                    IFSMState boot = FSMState.ConstructInstance(graph, Name(), string.Empty);
                    IFSMState waitForApple = FSMState.ConstructInstance(graph, Name(), string.Empty);
                    FSMUser user = User();
                    graph.InitialiseUser(user, null, boot);

                    IEnumerator wait = OfflineAppleSplash.CompleteAfterBoot(graph, boot, user, listener);
                    for (int frame = 0; frame < 3; frame++)
                    {
                        Assert.That(wait.MoveNext(), Is.True, "still in Boot, frame " + frame);
                        Assert.That(Get<bool>(listener, "m_forceUnload"), Is.False);
                        Assert.That(loadHandlers.GetValue(null), Is.Null);
                    }

                    graph.InitialiseUser(user, null, waitForApple);
                    Assert.That(wait.MoveNext(), Is.False);
                    Assert.That(Get<bool>(listener, "m_forceUnload"), Is.True, "Action_Unload(true), as the splash listener");
                    Delegate handlers = (Delegate)loadHandlers.GetValue(null);
                    Assert.That(handlers.GetInvocationList().Select(handler => handler.Target), Is.EqualTo(new object[] { listener }));
                    Assert.That(handlers.Method.Name, Is.EqualTo("OnPropertyStoreLoaded"));
                },
                () => loadHandlers.SetValue(null, null));
            });
        }

        // Arm waits on the scene loaded event for the named scene only, and leaves the event at that scene. A scene
        // with no splash VideoPlayer gets no call.
        [Test]
        public void ArmWaitsForTheNamedSceneAndLeavesTheEventThere()
        {
            FieldInfo loaded = typeof(SceneManager).GetField("sceneLoaded", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(loaded, Is.Not.Null);
            Delegate before = (Delegate)loaded.GetValue(null);
            Scene active = SceneManager.GetActiveScene();
            FiniteStateMachine graph = Machine();
            var boot = (FSMState)FSMState.ConstructInstance(graph, Name(), string.Empty);
            var armed = new System.Collections.Generic.List<Delegate>();
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                OfflineAppleSplash.Arm(boot, User(), active.name);
                Delegate named = Added(loaded, before, armed);
                Assert.That(named.Method.DeclaringType.FullName, Is.EqualTo("ProjectLucid.Offline.OfflineAppleSplash+PendingSplash"));
                Assert.That(named.Method.Name, Is.EqualTo("OnSceneLoaded"));
                OfflineAppleSplash.Arm(boot, User(), active.name + "-other");
                Delegate other = Added(loaded, before, armed);

                other.DynamicInvoke(active, LoadSceneMode.Additive);
                Assert.That(Handlers(loaded), Has.Member(other), "another scene leaves it waiting");

                Assert.That(active.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<VideoPlayer>(true)), Is.Empty);
                named.DynamicInvoke(active, LoadSceneMode.Additive);
                Assert.That(Handlers(loaded), Has.No.Member(named), "the named scene ends the wait");
                Assert.That(Handlers(loaded), Has.Member(other));
            },
            () =>
            {
                foreach (Delegate handler in armed)
                    SceneManager.sceneLoaded -= (UnityEngine.Events.UnityAction<Scene, LoadSceneMode>)handler;
            });
            Assert.That(Handlers(loaded), Is.EqualTo(Handlers(before)), "the event is as it was");
        }

        private static Delegate[] Handlers(FieldInfo field) => Handlers((Delegate)field.GetValue(null));

        private static Delegate[] Handlers(Delegate value) => value == null ? new Delegate[0] : value.GetInvocationList();

        // The one handler now on the event that is neither in before nor already recorded; it is recorded for cleanup.
        private static Delegate Added(FieldInfo field, Delegate before, System.Collections.Generic.List<Delegate> recorded)
        {
            Delegate[] added = Handlers(field).Where(handler => !Handlers(before).Contains(handler) && !recorded.Contains(handler)).ToArray();
            Assert.That(added.Length, Is.EqualTo(1));
            recorded.Add(added[0]);
            return added[0];
        }

        private static void DeleteIfPresent(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }
}
