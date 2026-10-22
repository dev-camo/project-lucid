using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
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
    //  - the launch button content stays inactive after OnEnable, and no full screen view is requested;
    //  - stepping Update on the enabled button, the base one and an unlocked CrossPromoLaunchButtonSDT (no
    //    unlock requirements), throws nothing and leaves the content inactive: the ad list is still the
    //    initial Array.Empty, and hook H-065 returns before the original's read past its end (0x1A18395,
    //    array bounds checks off in the shipped build).
    // All folders are unique owned ones under the persistent data path and are removed afterwards.
    // Not covered in EditMode: the real network request itself (the original path with the adapters off is
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
                    Assert.That(ProcessManager.IsSystemNull<CrossPromo>(), Is.True);
                    var iterator = (IEnumerator)Call(host, "Start");
                    Assert.That(iterator.MoveNext(), Is.False, "no wait on the core or the language, and no coroutine");
                    row.AdoptCurrent();

                    CrossPromo promo = ProcessManager.GetSystem<CrossPromo>();
                    Assert.That(Get<ICrossPromoResourceManager>(promo, "m_resourceManager").GetType().FullName,
                        Is.EqualTo("ProjectLucid.Offline.OfflineCrossPromoResourceManager"), "the constructor's metadata request went to the offline manager");
                    Assert.That(Get<ICrossPromoDataParser>(promo, "m_parser"), Is.TypeOf<CrossPromoDataParser>());
                    Assert.That(promo.IsReady(), Is.True);
                    Assert.That(promo.IsEnabled, Is.False, "the cache is not read");
                    Assert.That(Get<bool>(promo, "m_syncInProgress"), Is.False);
                    int initialisedCalls = 0;
                    CrossPromoMetaData received = default;
                    ((ICrossPromo)promo).Initialised += metaData => { initialisedCalls++; received = metaData; };
                    Assert.That(initialisedCalls, Is.EqualTo(1), "a ready system calls a late subscriber at once");
                    Assert.That(received.IsSystemEnabled, Is.False);
                    Assert.That(received.Ads, Is.Null);
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
                    StepUpdates(button, content, icon);

                    // The game's subclass, unlocked: no unlock requirements, so its gate is open and Update
                    // reaches TryShowCurrentAd with the empty ad list.
                    owned.LeaseRow<UIManager>();
                    GameObject sdtContent = owned.Host("Lucid SDT launch content", true);
                    GameObject sdtPlaceholder = owned.Host("Lucid SDT launch placeholder", false);
                    Image sdtIcon = owned.Host("Lucid SDT launch icon", true).AddComponent<Image>();
                    var sdt = owned.Host("Lucid SDT launch button", false).AddComponent<CrossPromoLaunchButtonSDT>();
                    Set(sdt, "m_content", sdtContent);
                    Set(sdt, "m_placeholderContent", sdtPlaceholder);
                    Set(sdt, "m_iconImage", sdtIcon);
                    Assert.That(Get<RequirementGameplayLevelBase[]>(sdt, "m_unlockDefinitions"), Is.Empty);
                    Assert.That(Call(sdt, "CanShowLaunchButton"), Is.True, "the button is unlocked");
                    Call(sdt, "OnEnable");
                    Assert.That(sdtContent.activeSelf, Is.False, "the disabled system shows no placeholder");
                    StepUpdates(sdt, sdtContent, sdtIcon);
                    Assert.That(sdtPlaceholder.activeSelf, Is.False);
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

        // The guard sits where the original reads the current ad: after the gate and the release of a previous
        // icon, before the icon request, which with the impression event stays in the original body after it.
        [Test]
        public void TryShowCurrentAdGuardsTheAdReadAndKeepsTheOriginalBody()
        {
            MethodInfo method = typeof(CrossPromoLaunchButton).GetMethod("TryShowCurrentAd",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            string[] names = OriginalILCallSequence.Names(OriginalILCallSequence.Calls(method));
            string[] expected =
            {
                "CrossPromoLaunchButton.CanShowLaunchButton", "Image.get_sprite", "CrossPromo.ReleaseIconTexture",
                "OfflineAdapters.get_Active", "CrossPromo.RequestIconTexture", "GameObject.SetActive", "CrossPromo.RaiseImpressionEvent",
            };
            int at = -1;
            foreach (string name in expected)
            {
                int next = Array.IndexOf(names, name, at + 1);
                Assert.That(next, Is.GreaterThan(at), name + " follows the previous call in IL order: " + string.Join(", ", names));
                at = next;
            }
            Assert.That(names.Count(name => name == "OfflineAdapters.get_Active"), Is.EqualTo(1));
        }

        // Update with the content hidden and the ad list non-null retries TryShowCurrentAd (0x1A18500); with the
        // empty list the offline hook returns before the icon request, every frame.
        private static void StepUpdates(CrossPromoLaunchButton button, GameObject content, Image icon)
        {
            Assert.That(Get<CrossPromoAd[]>(button, "m_ads"), Is.SameAs(Array.Empty<CrossPromoAd>()));
            Assert.That(Get<CrossPromo>(button, "m_crossPromo"), Is.Not.Null, "Update passes its system check");
            for (int frame = 0; frame < 3; frame++)
            {
                Assert.DoesNotThrow(() => Call(button, "Update"), "frame " + frame);
                Assert.That(content.activeSelf, Is.False, "frame " + frame);
                Assert.That(icon.sprite, Is.Null, "frame " + frame);
            }
        }

        private static void DeleteIfPresent(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }
}
