using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Offline;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Offline hooks of the boot flow: H-055 (the FairPlay check state), H-056 (the Apple Arcade splash
    // player, covered in OriginalAppleArcadeSplashScreenTests) and H-057 (cross promotion). Each case
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

        // HLCrossPromo.Start (0x0600186b) offline: no waits, no coroutine, no download manager and no
        // folders. The CrossPromo system is registered over the no-content resource manager, so it takes
        // CrossPromo's own "metadata is null or empty" path: ready, disabled, default metadata, and the
        // Initialised subscriber of a ready system is called at once.
        [Test]
        public void CrossPromoStartRegistersAnEmptyReadySystemWithoutNetworkOrFolders()
        {
            string suffix = Guid.NewGuid().ToString("N");
            string incoming = "lucid-test-incoming-" + suffix, destination = "lucid-test-destination-" + suffix;
            Owned.Run(owned =>
            {
                owned.Lease(new OwnedCrossPromoConfiguration("{\"m_incomingFolder\":\"" + incoming + "\",\"m_destinationFolder\":\"" +
                    destination + "\",\"m_canUseLocalStorage\":true,\"m_gameTitle\":\"lucid-title\"}"));
                var row = owned.Lease(new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<CrossPromo>()));
                row.Register(null);
                VerificationFixtureOwnership.RunThenCleanUp(() =>
                {
                    var host = owned.Host("Lucid cross promo", false).AddComponent<HLCrossPromo>();
                    Assert.That(ProcessManager.IsSystemNull<CrossPromo>(), Is.True);
                    var iterator = (IEnumerator)Call(host, "Start");
                    Assert.That(iterator.MoveNext(), Is.False, "no wait on the core or the language");
                    row.AdoptCurrent();

                    CrossPromo promo = ProcessManager.GetSystem<CrossPromo>();
                    ICrossPromoResourceManager resources = Get<ICrossPromoResourceManager>(promo, "m_resourceManager");
                    Assert.That(resources.GetType().FullName, Is.EqualTo("ProjectLucid.Offline.OfflineCrossPromoResourceManager"));
                    Assert.That(Get<ICrossPromoDataParser>(promo, "m_parser"), Is.TypeOf<CrossPromoDataParser>());
                    Assert.That(promo.IsReady(), Is.True);
                    Assert.That(promo.IsEnabled, Is.False);
                    Assert.That(Get<bool>(promo, "m_syncInProgress"), Is.False);

                    int calls = 0;
                    CrossPromoMetaData received = default;
                    ((ICrossPromo)promo).Initialised += metaData => { calls++; received = metaData; };
                    Assert.That(calls, Is.EqualTo(1), "a ready system calls a late subscriber at once");
                    Assert.That(received.IsSystemEnabled, Is.False);
                    Assert.That(received.Ads, Is.Null);

                    Assert.That(Directory.Exists(Path.Combine(Application.persistentDataPath, incoming)), Is.False);
                    Assert.That(Directory.Exists(Path.Combine(Application.persistentDataPath, destination)), Is.False);
                    UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
                },
                () => DeleteIfPresent(Path.Combine(Application.persistentDataPath, incoming)),
                () => DeleteIfPresent(Path.Combine(Application.persistentDataPath, destination)));
            });
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

        private static void DeleteIfPresent(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }
}
