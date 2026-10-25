using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Offline;
using ProjectLucid.Tests.CrossPromoUIShared;
using UnityEngine;
using UnityEngine.UI;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // CrossPromoLaunchButtonSDT (TypeDef 0x02000998, x86-64 0x698E00..0x699180) on an inactive owned host over a
    // real CrossPromo, a real UIManager with a recording container manager and an owned CrossPromoConfigurationAsset.
    // The probe subclass records the virtual full screen request the launch action makes.
    public sealed class CrossPromoLaunchButtonSdtTests
    {
        private static readonly MethodInfo ShouldUseCachedData =
            typeof(CrossPromoLaunchButtonSDT).GetMethod("ShouldUseCachedData", BindingFlags.Static | BindingFlags.NonPublic);

        private sealed class Scene
        {
            public CrossPromoGameScene Game;
            public GameObject Content, Placeholder, IconObject;
            public Image Icon;

            public T NewButton<T>(Owned owned) where T : CrossPromoLaunchButtonSDT
            {
                var button = owned.Host("Lucid launch button", false).AddComponent<T>();
                Content = owned.Host("Lucid launch content", true);
                Placeholder = owned.Host("Lucid launch placeholder", true);
                IconObject = owned.Host("Lucid launch icon", true);
                Icon = IconObject.AddComponent<Image>();
                Set(button, "m_content", Content);
                Set(button, "m_placeholderContent", Placeholder);
                Set(button, "m_iconImage", Icon);
                return button;
            }
        }

        // The configuration is leased first, so it is released last.
        private static void Run(bool canUseLocalStorage, bool promoEnabled, Action<Owned, Scene> body) => Owned.Run(owned =>
        {
            // Original 0x06003787 @0x698E00 shows cached content; D14 hiding is tested separately with the adapter on.
            using (OfflineAdapters.Override(false))
            {
                owned.Lease(new OwnedCrossPromoConfiguration("{\"m_canUseLocalStorage\":" + (canUseLocalStorage ? "true" : "false") + "}"));
                var scene = new Scene { Game = new CrossPromoGameScene(owned, promoEnabled, "a", "b") };
                body(owned, scene);
            }
        });

        // dump.cs TypeDefIndex 2455 and the DummyDll rows (fields 0x04002825..0x04002829).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(CrossPromoLaunchButtonSDT), 0x00100001,
                new (string, int)[]
                {
                    ("m_crossPromoContainerIdentifier", 0x0001),
                    ("m_unlockAtLevel", 0x0001),
                    ("m_unlockDefinitions", 0x0001),
                    ("m_placeholderContent", 0x0001),
                    ("m_uiManagerRef", 0x0021),
                },
                new (string, int)[]
                {
                    ("OnEnable", 0x00c4),
                    ("ShouldUseCachedData", 0x0091),
                    ("RequestFullScreenView", 0x00c4),
                    ("CanShowLaunchButton", 0x00c4),
                    ("SetPlaceHolderContentActive", 0x0081),
                    ("Action_LaunchCrossPromo", 0x0086),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(CrossPromoLaunchButtonSDT), 1, 2);
            Assert.That(typeof(CrossPromoLaunchButtonSDT).BaseType, Is.EqualTo(typeof(CrossPromoLaunchButton)));
            foreach (string field in new[] { "m_crossPromoContainerIdentifier", "m_unlockAtLevel", "m_unlockDefinitions", "m_placeholderContent" })
                Assert.That(AttributeTypes(typeof(CrossPromoLaunchButtonSDT).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)),
                    Is.EqualTo(new[] { typeof(SerializeField) }), field);
        }

        // The constructor 0x699040 stores the shared empty requirement array at +0x68 and the UIManager reference at
        // +0x78 before the base constructor.
        [Test]
        public void ConstructorStartsWithNoRequirementsAndTheRegistryUiManager()
        {
            Run(true, true, (owned, scene) =>
            {
                var button = scene.NewButton<CrossPromoLaunchButtonSDT>(owned);
                Assert.That(Get<RequirementGameplayLevelBase[]>(button, "m_unlockDefinitions"), Is.SameAs(Array.Empty<RequirementGameplayLevelBase>()));
                Assert.That(Get<SystemRef<UIManager>>(button, "m_uiManagerRef").Get(), Is.SameAs(scene.Game.Manager));
            });
        }

        // CanShowLaunchButton 0x698FE0 tail-calls Requirements.AreMet on the authored array with no level: each
        // requirement is asked in order, the first refusal ends the walk and an empty array is met.
        [Test]
        public void TheLaunchButtonShowsOnlyWhileEveryUnlockRequirementIsMet()
        {
            Run(true, true, (owned, scene) =>
            {
                var button = scene.NewButton<CrossPromoLaunchButtonSDT>(owned);
                var log = new List<string>();
                CrossPromoUnlockRequirementProbe Requirement(string label, bool met)
                {
                    var probe = Create<CrossPromoUnlockRequirementProbe>(owned);
                    probe.Label = label;
                    probe.Met = met;
                    probe.Log = log;
                    return probe;
                }

                Assert.That(Call(button, "CanShowLaunchButton"), Is.True, "no requirements");
                CrossPromoUnlockRequirementProbe met = Requirement("met", true), unmet = Requirement("unmet", false), late = Requirement("late", true);
                Set(button, "m_unlockDefinitions", new RequirementGameplayLevelBase[] { met });
                Assert.That(Call(button, "CanShowLaunchButton"), Is.True);
                Assert.That(met.Received, Is.Null, "no level definition is passed");
                Set(button, "m_unlockDefinitions", new RequirementGameplayLevelBase[] { unmet });
                Assert.That(Call(button, "CanShowLaunchButton"), Is.False);
                log.Clear();
                Set(button, "m_unlockDefinitions", new RequirementGameplayLevelBase[] { met, unmet, late });
                Assert.That(Call(button, "CanShowLaunchButton"), Is.False);
                Assert.That(log, Is.EqualTo(new[] { "met", "unmet" }), "the walk stops at the first refusal");
                log.Clear();
                Set(button, "m_unlockDefinitions", new RequirementGameplayLevelBase[] { late, met });
                Assert.That(Call(button, "CanShowLaunchButton"), Is.True);
                Assert.That(log, Is.EqualTo(new[] { "late", "met" }));
            });
        }

        // SetPlaceHolderContentActive 0x698EF0: the content is always activated, the placeholder follows the flag and
        // the icon image object takes the opposite state.
        [Test]
        public void ThePlaceholderReplacesTheIconWhileTheContentStaysActive()
        {
            Run(true, true, (owned, scene) =>
            {
                var button = scene.NewButton<CrossPromoLaunchButtonSDT>(owned);
                scene.Content.SetActive(false);
                scene.Placeholder.SetActive(false);
                scene.IconObject.SetActive(true);
                Call(button, "SetPlaceHolderContentActive", true);
                Assert.That((scene.Content.activeSelf, scene.Placeholder.activeSelf, scene.IconObject.activeSelf), Is.EqualTo((true, true, false)));
                Call(button, "SetPlaceHolderContentActive", false);
                Assert.That((scene.Content.activeSelf, scene.Placeholder.activeSelf, scene.IconObject.activeSelf), Is.EqualTo((true, false, true)));
            });
        }

        // ShouldUseCachedData 0x698E60: false whenever the configuration's local-storage flag (+0x68) is set;
        // otherwise the registered CrossPromo's IsEnabled. Truth table: (local storage, system enabled, result).
        [TestCase(true, true, false, TestName = "CachedDataIsNotUsedWithLocalStorageAndAnEnabledSystem")]
        [TestCase(true, false, false, TestName = "CachedDataIsNotUsedWithLocalStorageAndADisabledSystem")]
        [TestCase(false, true, true, TestName = "CachedDataIsUsedWithoutLocalStorageForAnEnabledSystem")]
        [TestCase(false, false, false, TestName = "CachedDataIsNotUsedWithoutLocalStorageForADisabledSystem")]
        public void CachedDataIsUsedOnlyWithoutLocalStorageForAnEnabledSystem(bool localStorage, bool enabled, bool expected)
        {
            Run(localStorage, enabled, (owned, scene) =>
            {
                Assert.That(scene.Game.Promo.IsEnabled, Is.EqualTo(enabled), "the scripted system");
                Assert.That(ShouldUseCachedData.Invoke(null, null), Is.EqualTo(expected));
            });
        }

        // OnEnable 0x698E00 runs the base enable (which hides the content) and then, when cached data is to be used,
        // shows the placeholder instead of the icon (the same three activations as SetPlaceHolderContentActive(true)).
        [TestCase(false, true, true, TestName = "EnableShowsThePlaceholderForCachedData")]
        [TestCase(true, true, false, TestName = "EnableLeavesTheBaseHiddenContentWithLocalStorage")]
        [TestCase(false, false, false, TestName = "EnableLeavesTheBaseHiddenContentForADisabledSystem")]
        public void EnableShowsThePlaceholderOnlyForCachedData(bool localStorage, bool enabled, bool placeholderShown)
        {
            Run(localStorage, enabled, (owned, scene) =>
            {
                var button = scene.NewButton<CrossPromoLaunchButtonSDT>(owned);
                scene.Content.SetActive(true);
                scene.Placeholder.SetActive(false);
                scene.IconObject.SetActive(true);
                Call(button, "OnEnable");
                Assert.That(Get<CrossPromo>(button, "m_crossPromo"), Is.SameAs(scene.Game.Promo), "the base enable took the system");
                Assert.That(scene.Content.activeSelf, Is.EqualTo(placeholderShown), "the base hides the content; cached data shows it");
                Assert.That(scene.Placeholder.activeSelf, Is.EqualTo(placeholderShown));
                Assert.That(scene.IconObject.activeSelf, Is.EqualTo(!placeholderShown));
            });
        }

        // Action_LaunchCrossPromo 0x699000 hides the base action: with cached data it makes the virtual full screen
        // request (slot 7) for ad 0 whatever the current ad, and does not rotate; otherwise it runs the base action,
        // which requests the current ad.
        [Test]
        public void LaunchRequestsTheFirstAdForCachedDataAndTheCurrentAdOtherwise()
        {
            Run(false, true, (owned, scene) =>
            {
                var button = scene.NewButton<CrossPromoLaunchButtonSdtProbe>(owned);
                Set(button, "m_currentAd", 1);
                button.Action_LaunchCrossPromo();
                Assert.That(button.Requested, Is.EqualTo(new[] { 0 }), "cached data always starts at the first ad");
                Assert.That(Get<int>(button, "m_currentAd"), Is.EqualTo(1));
            });
            Run(true, true, (owned, scene) =>
            {
                var button = scene.NewButton<CrossPromoLaunchButtonSdtProbe>(owned);
                Set(button, "m_currentAd", 1);
                button.Action_LaunchCrossPromo();
                Assert.That(button.Requested, Is.EqualTo(new[] { 1 }), "the base action requests the current ad");
            });
            Run(false, false, (owned, scene) =>
            {
                var button = scene.NewButton<CrossPromoLaunchButtonSdtProbe>(owned);
                Set(button, "m_currentAd", 1);
                button.Action_LaunchCrossPromo();
                Assert.That(button.Requested, Is.EqualTo(new[] { 1 }), "a disabled system is not cached data");
            });
        }

        // RequestFullScreenView 0x698F40 looks the UIManager up first, then calls GetOrCreate for the authored
        // identifier with new UIContainerCrossPromoParameters(adIndex): the container is opened and its setup shows
        // the requested ad in its full screen view.
        [Test]
        public void RequestingTheFullScreenViewOpensTheCrossPromoContainerOnThatAd()
        {
            Run(true, true, (owned, scene) =>
            {
                UIContainerCrossPromo container = scene.Game.NewContainer("promo");
                var button = scene.NewButton<CrossPromoLaunchButtonSDT>(owned);
                Set(button, "m_crossPromoContainerIdentifier", container.Identifier);
                scene.Game.Log.Clear();
                Call(button, "RequestFullScreenView", 1);
                Assert.That(scene.Game.Log, Is.EqualTo(new[] { "open:promo" }));
                var view = Get<CrossPromoFullScreenView>(container, "m_fullScreenView");
                Assert.That(CrossPromoGameScene.CurrentAd(view), Is.EqualTo(1));
                Assert.That(view.gameObject.activeSelf, Is.True);
            });
        }
    }
}
