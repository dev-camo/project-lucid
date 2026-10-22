using System;
using System.Collections;
using System.IO;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Offline hook H-057 (HLCrossPromo.Start) end to end with the adapters active, in the four cases of
    // {cold start, existing cache on disk} x {local storage usable, not usable}. After Start, a real
    // CrossPromoLaunchButton is enabled on an owned host. In every case:
    //  - the CrossPromo constructor's metadata request goes to the offline resource manager, never to the
    //    disk-backed or cache-only one (the type is checked), so no download manager and no coroutine
    //    exist: a refused coroutine on the inactive host would be an unexpected error log and fail the test;
    //  - no folder is created, and an existing cache file is neither read nor changed (the system stays
    //    disabled with no ads although the file would enable it);
    //  - the launch button content stays inactive after OnEnable, and no full screen view is requested.
    // All folders are unique owned ones under the persistent data path and are removed afterwards.
    // Not covered in EditMode: the button's Update (the original reads the first element of its empty ad
    // list there, with array bounds checks off in the shipped build, so it is not a stable thing to step),
    // the real network request itself (the original path with the adapters off is
    // covered by HLCrossPromoStartTests), and the launch button inside a running player loop.
    public sealed class OfflineCrossPromoHookTests
    {
        private const string CacheText = "{\"lucid\":{\"IsSystemEnabled\":true}}";

        private static void Run(bool canUseLocalStorage, bool cacheOnDisk)
        {
            string suffix = Guid.NewGuid().ToString("N");
            string incoming = "lucid-test-incoming-" + suffix, destination = "lucid-test-destination-" + suffix;
            string incomingPath = Path.Combine(Application.persistentDataPath, incoming);
            string destinationPath = Path.Combine(Application.persistentDataPath, destination);
            string cacheFile = Path.Combine(destinationPath, "settings.json");
            Owned.Run(owned =>
            {
                owned.Lease(new OwnedCrossPromoConfiguration("{\"m_incomingFolder\":\"" + incoming + "\",\"m_destinationFolder\":\"" +
                    destination + "\",\"m_canUseLocalStorage\":" + (canUseLocalStorage ? "true" : "false") + ",\"m_gameTitle\":\"lucid-title\"}"));
                var row = owned.Lease(new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<CrossPromo>()));
                row.Register(null);
                VerificationFixtureOwnership.RunThenCleanUp(() =>
                {
                    if (cacheOnDisk)
                    {
                        Directory.CreateDirectory(destinationPath);
                        File.WriteAllText(cacheFile, CacheText);
                    }

                    var host = owned.Host("Lucid cross promo", false).AddComponent<HLCrossPromo>();
                    var iterator = (IEnumerator)Call(host, "Start");
                    Assert.That(iterator.MoveNext(), Is.False, "no wait and no coroutine");
                    row.AdoptCurrent();

                    CrossPromo promo = ProcessManager.GetSystem<CrossPromo>();
                    Assert.That(Get<ICrossPromoResourceManager>(promo, "m_resourceManager").GetType().FullName,
                        Is.EqualTo("ProjectLucid.Offline.OfflineCrossPromoResourceManager"), "the constructor's metadata request went to the offline manager");
                    Assert.That(promo.IsReady(), Is.True);
                    Assert.That(promo.IsEnabled, Is.False, "the cache is not read");
                    int fullScreenRequests = 0;
                    promo.FullScreenViewRequested += (metaData, index) => fullScreenRequests++;

                    GameObject content = owned.Host("Lucid launch content", true);
                    GameObject iconObject = owned.Host("Lucid launch icon", true);
                    Image icon = iconObject.AddComponent<Image>();
                    var button = owned.Host("Lucid launch button", false).AddComponent<CrossPromoLaunchButton>();
                    Set(button, "m_content", content);
                    Set(button, "m_iconImage", icon);
                    // EditMode does not send OnEnable, so the enable step is invoked directly, as
                    // OriginalCrossPromoLaunchButtonTests does. It hides the content and, the system being ready,
                    // takes the immediate Initialised callback with the disabled metadata.
                    Assert.That(content.activeSelf, Is.True);
                    Call(button, "OnEnable");
                    Assert.That(content.activeSelf, Is.False, "the launch button stays disabled after enabling");
                    Assert.That(icon.sprite, Is.Null);
                    Assert.That(fullScreenRequests, Is.EqualTo(0), "no ad UI is requested");

                    Assert.That(Directory.Exists(incomingPath), Is.False, "no incoming folder");
                    Assert.That(Directory.Exists(destinationPath), Is.EqualTo(cacheOnDisk), "no destination folder is created");
                    if (cacheOnDisk) Assert.That(File.ReadAllText(cacheFile), Is.EqualTo(CacheText), "the cache is untouched");
                    LogAssert.NoUnexpectedReceived();
                },
                () => DeleteIfPresent(incomingPath),
                () => DeleteIfPresent(destinationPath));
            });
        }

        [Test]
        public void ColdStartWithLocalStorageStaysDisabledWithoutRequests() => Run(true, false);

        [Test]
        public void ColdStartWithoutLocalStorageStaysDisabledWithoutRequests() => Run(false, false);

        [Test]
        public void ExistingCacheWithLocalStorageStaysDisabledWithoutRequests() => Run(true, true);

        [Test]
        public void ExistingCacheWithoutLocalStorageStaysDisabledWithoutRequests() => Run(false, true);

        private static void DeleteIfPresent(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }
}
