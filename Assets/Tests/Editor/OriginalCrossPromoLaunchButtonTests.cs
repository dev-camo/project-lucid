using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using ProjectLucid.Tests.CrossPromoUIShared;
using UnityEngine;
using UnityEngine.UI;
using static ProjectLucid.Tests.CrossPromoUiAccess;

namespace ProjectLucid.Tests
{
    // Hardlight.CrossPromoLaunchButton (TypeDef 0x0200001f, x86-64 0x1A17F00..0x1A18860) on an
    // inactive owned button with a content child and an icon Image. The probe subclass steers the
    // original virtual CanShowLaunchButton gate; the CrossPromo system is the real class over an
    // in-memory resource manager that hands out fixture sprites.
    public sealed class OriginalCrossPromoLaunchButtonTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly CrossPromoUiObjects Objects = new CrossPromoUiObjects("lucid-crosspromo-launch-button");
            public readonly CrossPromoLaunchButton Button;
            public readonly GameObject Content;
            public readonly Image Icon;
            public readonly RecordingResourceManager Resources = new RecordingResourceManager();
            public readonly List<string> Events = new List<string>();
            public CrossPromo Promo;

            public Fixture(Type buttonType)
            {
                Button = (CrossPromoLaunchButton)Objects.Child("button", null, buttonType).GetComponent(buttonType);
                Content = Objects.Child("content", Button.transform);
                Icon = Objects.Child("icon", Content.transform, typeof(Image)).GetComponent<Image>();
                Set(Button, "m_content", Content);
                Set(Button, "m_iconImage", Icon);
            }

            public CrossPromoLaunchButtonProbe Probe => (CrossPromoLaunchButtonProbe)Button;

            // Hands the button a real CrossPromo the way InitialiseCrossPromo stores it, without
            // going through the registry; its events report into Events.
            public CrossPromo UsePromo()
            {
                Promo = NewCrossPromo(Resources);
                Promo.LaunchButtonShown += (ad, count) => Events.Add("LaunchButtonShown(" + ad.Name + "," + count + ")");
                Promo.FullScreenViewRequested += (meta, index) => Events.Add("FullScreenViewRequested(" + index + ")");
                Set(Button, "m_crossPromo", Promo);
                return Promo;
            }

            public object Invoke(string method, params object[] args) => Call(typeof(CrossPromoLaunchButton), Button, method, args);

            public int Int(string field) => (int)Get(Button, field);

            public void Dispose() => Objects.Dispose();
        }

        private static CrossPromoMetaData Meta(bool enabled, CrossPromoAd[] ads, string firstAd = null) =>
            new CrossPromoMetaData { IsSystemEnabled = enabled, Ads = ads, FirstAd = firstAd };

        // dump.cs TypeDefIndex 13734 and the DummyDll rows; .ctor 0x1A18860 sets the force flag and
        // an Array.Empty ad list; CanShowLaunchButton 0x1A18570 is true.
        [Test]
        public void DeclarationsDefaultsAndTheBaseGate()
        {
            Type type = typeof(CrossPromoLaunchButton);
            Assert.That((int)type.Attributes, Is.EqualTo(0x00100001));
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            FieldInfo[] fields = type.GetFields(all);
            Assert.That(fields.Select(f => f.Name + ":" + f.FieldType.Name + ":" + (int)f.Attributes), Is.EqualTo(new[]
            {
                "m_content:GameObject:4", "m_iconImage:Image:4", "m_forceUseFirstAdIndexOnInitialise:Boolean:1",
                "m_crossPromo:CrossPromo:1", "m_crossPromoRef:SystemRef`1:1", "m_ads:CrossPromoAd[]:1",
                "m_currentAd:Int32:1", "m_impressionCount:Int32:1"
            }));
            // Attribute rows of 0x0400009f..0x040000a1 in DummyDll metadata order; the rest have none.
            Assert.That(fields.Select(f => string.Join(" ", AttributeRows(f))), Is.EqualTo(new[]
            {
                "SerializeField() TooltipAttribute(The content that owns the launch button visuals)",
                "SerializeField() TooltipAttribute(The image to which the icon texture is injected)",
                "TooltipAttribute(True if the ads should be reset on initialise. If your launch button gets disabled during full screen display, this should be false.) SerializeField()",
                "", "", "", "", ""
            }));
            var virtuals = new[] { "OnEnable", "OnCrossPromoInitialized", "CanShowLaunchButton", "RequestFullScreenView" };
            foreach (string name in virtuals)
                Assert.That((int)type.GetMethod(name, all).Attributes, Is.EqualTo(0x01c4), name);
            foreach (string name in new[] { "InitialiseCrossPromo", "Update", "TryShowCurrentAd", "OnDisable" })
                Assert.That((int)type.GetMethod(name, all).Attributes, Is.EqualTo(0x0081), name);

            using (var fixture = new Fixture(typeof(CrossPromoLaunchButton)))
            {
                Assert.That(Get(fixture.Button, "m_forceUseFirstAdIndexOnInitialise"), Is.True);
                Assert.That(Get(fixture.Button, "m_ads"), Is.SameAs(Array.Empty<CrossPromoAd>()));
                Assert.That(fixture.Int("m_currentAd"), Is.EqualTo(0));
                Assert.That(fixture.Int("m_impressionCount"), Is.EqualTo(0));
                Assert.That(fixture.Invoke("CanShowLaunchButton"), Is.True);
            }
        }

        // OnCrossPromoInitialized 0x1A181B0: a disabled system or a null or empty ad list changes
        // nothing. Otherwise the impression count resets, the list is taken and the current ad
        // becomes GetFirstAdIndex unless the force flag is clear and the old list is the very same
        // array (Array.Equals is reference equality), in which case it is kept. The probe's closed
        // gate makes the trailing TryShowCurrentAd return at once.
        [Test]
        public void InitialisedResetsImpressionsAndKeepsTheAdOnlyForTheSameList()
        {
            using (var fixture = new Fixture(typeof(CrossPromoLaunchButtonProbe)))
            {
                CrossPromoAd[] first = Ads("a", "b", "c");
                Set(fixture.Button, "m_ads", first);
                Set(fixture.Button, "m_currentAd", 2);
                Set(fixture.Button, "m_impressionCount", 5);
                foreach (CrossPromoMetaData ignored in new[] { Meta(false, Ads("x")), Meta(true, null), Meta(true, new CrossPromoAd[0]) })
                {
                    fixture.Invoke("OnCrossPromoInitialized", ignored);
                    Assert.That(Get(fixture.Button, "m_ads"), Is.SameAs(first));
                    Assert.That(fixture.Int("m_currentAd"), Is.EqualTo(2));
                    Assert.That(fixture.Int("m_impressionCount"), Is.EqualTo(5));
                }

                Set(fixture.Button, "m_forceUseFirstAdIndexOnInitialise", false);
                fixture.Invoke("OnCrossPromoInitialized", Meta(true, first, "a"));
                Assert.That(fixture.Int("m_currentAd"), Is.EqualTo(2), "the same list keeps the current ad");
                Assert.That(fixture.Int("m_impressionCount"), Is.EqualTo(0));

                CrossPromoAd[] equalCopy = Ads("a", "b", "c");
                fixture.Invoke("OnCrossPromoInitialized", Meta(true, equalCopy, "b"));
                Assert.That(Get(fixture.Button, "m_ads"), Is.SameAs(equalCopy));
                Assert.That(fixture.Int("m_currentAd"), Is.EqualTo(1), "an equal but different list restarts at the first ad");

                Set(fixture.Button, "m_ads", null);
                Set(fixture.Button, "m_currentAd", 2);
                fixture.Invoke("OnCrossPromoInitialized", Meta(true, equalCopy, "b"));
                Assert.That(fixture.Int("m_currentAd"), Is.EqualTo(1), "no previous list restarts at the first ad");

                Set(fixture.Button, "m_forceUseFirstAdIndexOnInitialise", true);
                Set(fixture.Button, "m_currentAd", 2);
                fixture.Invoke("OnCrossPromoInitialized", Meta(true, equalCopy, "b"));
                Assert.That(fixture.Int("m_currentAd"), Is.EqualTo(1), "the force flag restarts even for the same list");
                Assert.That(fixture.Resources.Log, Is.Empty);
            }
        }

        // OnCrossPromoInitialized 0x1A181B0 ends with a tail call to TryShowCurrentAd (jmp
        // 0x1A18270 at 0x1A18214), after the impression count is reset (0x1A181DB) and the first
        // ad index is stored (0x1A18200): the shown ad is "b" with impression 0, not "a" or 5.
        [Test]
        public void InitialisedShowsTheFirstAdWithAFreshImpressionCount()
        {
            using (var fixture = new Fixture(typeof(CrossPromoLaunchButtonProbe)))
            {
                fixture.Probe.CanShow = true;
                fixture.UsePromo();
                Sprite iconB = fixture.Objects.Sprite("icon-b");
                fixture.Resources.IconSprites = texture => texture.URL == "icon-b" ? iconB : null;
                Set(fixture.Button, "m_impressionCount", 5);
                Set(fixture.Button, "m_currentAd", 0);
                fixture.Content.SetActive(false);
                fixture.Resources.Log.Clear();

                fixture.Invoke("OnCrossPromoInitialized", Meta(true, Ads("a", "b"), "b"));

                Assert.That(fixture.Resources.Log, Is.EqualTo(new[] { "RequestIconTexture(icon-b)" }));
                Assert.That(fixture.Events, Is.EqualTo(new[] { "LaunchButtonShown(b,0)" }));
                Assert.That(fixture.Int("m_impressionCount"), Is.EqualTo(1));
                Assert.That(fixture.Int("m_currentAd"), Is.EqualTo(1));
                Assert.That(fixture.Icon.sprite, Is.SameAs(iconB));
                Assert.That(fixture.Content.activeSelf, Is.True);
            }
        }

        // TryShowCurrentAd 0x1A18270: behind the CanShowLaunchButton gate it clears and releases
        // the shown icon, requests the current ad's icon, and only for a non-null icon shows it,
        // activates the content and raises LaunchButtonShown with the count before incrementing it.
        [Test]
        public void ShowingAnAdSwapsTheIconAndCountsImpressionsOnlyWhenOneIsReturned()
        {
            using (var fixture = new Fixture(typeof(CrossPromoLaunchButtonProbe)))
            {
                fixture.UsePromo();
                Sprite stale = fixture.Objects.Sprite("stale");
                Sprite iconB = fixture.Objects.Sprite("icon-b");
                fixture.Resources.IconSprites = texture => texture.URL == "icon-b" ? iconB : null;
                fixture.Resources.Log.Clear();
                Set(fixture.Button, "m_ads", Ads("a", "b"));
                Set(fixture.Button, "m_currentAd", 1);
                fixture.Icon.sprite = stale;
                fixture.Content.SetActive(false);

                fixture.Invoke("TryShowCurrentAd");
                Assert.That(fixture.Resources.Log, Is.Empty, "a closed gate does nothing");
                Assert.That(fixture.Icon.sprite, Is.SameAs(stale));

                fixture.Probe.CanShow = true;
                fixture.Invoke("TryShowCurrentAd");
                Assert.That(fixture.Resources.Log, Is.EqualTo(new[] { "ReleaseIconTexture(stale)", "RequestIconTexture(icon-b)" }));
                Assert.That(fixture.Icon.sprite, Is.SameAs(iconB));
                Assert.That(fixture.Content.activeSelf, Is.True);
                Assert.That(fixture.Events, Is.EqualTo(new[] { "LaunchButtonShown(b,0)" }));
                Assert.That(fixture.Int("m_impressionCount"), Is.EqualTo(1));

                fixture.Resources.Log.Clear();
                fixture.Content.SetActive(false);
                Set(fixture.Button, "m_currentAd", 0);
                fixture.Invoke("TryShowCurrentAd");
                Assert.That(fixture.Resources.Log, Is.EqualTo(new[] { "ReleaseIconTexture(icon-b)", "RequestIconTexture(icon-a)" }));
                Assert.That(fixture.Icon.sprite, Is.Null);
                Assert.That(fixture.Content.activeSelf, Is.False);
                Assert.That(fixture.Events.Count, Is.EqualTo(1));
                Assert.That(fixture.Int("m_impressionCount"), Is.EqualTo(1));

                fixture.Resources.Log.Clear();
                fixture.Invoke("TryShowCurrentAd");
                Assert.That(fixture.Resources.Log, Is.EqualTo(new[] { "RequestIconTexture(icon-a)" }), "an empty icon is not released");
            }
        }

        // Action_LaunchCrossPromo 0x1A18810 requests the full screen view on the current ad through
        // RequestFullScreenView 0x1A18580, then rotates to the next ad (wrapping) only when there are
        // at least two.
        [Test]
        public void LaunchRequestsTheCurrentAdThenRotates()
        {
            using (var fixture = new Fixture(typeof(CrossPromoLaunchButtonProbe)))
            {
                fixture.UsePromo();
                Set(fixture.Button, "m_ads", Ads("a", "b", "c"));
                Set(fixture.Button, "m_currentAd", 1);
                var visited = new List<int>();
                for (int i = 0; i < 3; i++)
                {
                    fixture.Button.Action_LaunchCrossPromo();
                    visited.Add(fixture.Int("m_currentAd"));
                }
                Assert.That(fixture.Events, Is.EqualTo(new[] { "FullScreenViewRequested(1)", "FullScreenViewRequested(2)", "FullScreenViewRequested(0)" }));
                Assert.That(visited, Is.EqualTo(new[] { 2, 0, 1 }));

                fixture.Events.Clear();
                Set(fixture.Button, "m_ads", Ads("only"));
                Set(fixture.Button, "m_currentAd", 0);
                fixture.Button.Action_LaunchCrossPromo();
                Assert.That(fixture.Events, Is.EqualTo(new[] { "FullScreenViewRequested(0)" }));
                Assert.That(fixture.Int("m_currentAd"), Is.EqualTo(0));
                Assert.That(fixture.Resources.Log.Where(entry => entry.StartsWith("RequestIconTexture")), Is.Empty, "the closed gate shows nothing");
            }
        }

        // Update 0x1A18500: nothing without a system; shown content is hidden once the gate
        // closes; hidden content retries TryShowCurrentAd only with an ad list.
        [Test]
        public void UpdateHidesOnAClosedGateAndRetriesHiddenContent()
        {
            using (var fixture = new Fixture(typeof(CrossPromoLaunchButtonProbe)))
            {
                Set(fixture.Button, "m_ads", null);
                fixture.Invoke("Update");
                Assert.That(fixture.Content.activeSelf, Is.True, "no system: nothing happens");

                fixture.UsePromo();
                fixture.Invoke("Update");
                Assert.That(fixture.Content.activeSelf, Is.False, "the closed gate hides shown content");

                fixture.Probe.CanShow = true;
                fixture.Resources.Log.Clear();
                fixture.Invoke("Update");
                Assert.That(fixture.Resources.Log, Is.Empty, "hidden content without an ad list is left");

                Sprite icon = fixture.Objects.Sprite("icon-a");
                fixture.Resources.IconSprites = texture => icon;
                Set(fixture.Button, "m_ads", Ads("a"));
                fixture.Invoke("Update");
                Assert.That(fixture.Resources.Log, Is.EqualTo(new[] { "RequestIconTexture(icon-a)" }));
                Assert.That(fixture.Content.activeSelf, Is.True);

                fixture.Resources.Log.Clear();
                fixture.Invoke("Update");
                Assert.That(fixture.Resources.Log, Is.Empty, "shown content with an open gate is left");
            }
        }

        // OnEnable 0x1A17F00 hides the content and waits on OnSystemStartup; registration runs
        // InitialiseCrossPromo 0x1A18050, which keeps the system, leaves OnSystemStartup and
        // subscribes the virtual OnCrossPromoInitialized to Initialised. OnDisable 0x1A185C0 hides
        // the content, leaves Initialised and releases a shown icon.
        [Test]
        public void EnableWaitsForTheSystemAndDisableLeavesInitialised()
        {
            using (var fixture = new Fixture(typeof(CrossPromoLaunchButtonProbe)))
            using (var row = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<CrossPromo>()))
            {
                row.Register(null);
                fixture.Invoke("OnEnable");
                Assert.That(fixture.Content.activeSelf, Is.False);
                var reference = (SystemRef<CrossPromo>)Get(fixture.Button, "m_crossPromoRef");
                Assert.That(reference, Is.SameAs(ProcessManager.GetSystemRef<CrossPromo>()));
                Assert.That(Get(fixture.Button, "m_crossPromo"), Is.Null);
                Delegate waiting = Handlers(reference, "OnSystemStartup").Single();
                Assert.That(waiting.Target, Is.SameAs(fixture.Button));
                Assert.That(waiting.Method.Name, Is.EqualTo("InitialiseCrossPromo"));

                CrossPromo promo = NewCrossPromo(fixture.Resources);
                row.Register(promo);
                Assert.That(Get(fixture.Button, "m_crossPromo"), Is.SameAs(promo));
                Assert.That(Handlers(reference, "OnSystemStartup"), Is.Empty);
                Delegate initialised = Handlers(promo, "Initialised").Single();
                Assert.That(initialised.Target, Is.SameAs(fixture.Button));
                Assert.That(initialised.Method.Name, Is.EqualTo("OnCrossPromoInitialized"));

                Sprite icon = fixture.Objects.Sprite("shown");
                fixture.Icon.sprite = icon;
                fixture.Content.SetActive(true);
                fixture.Resources.Log.Clear();
                fixture.Invoke("OnDisable");
                Assert.That(fixture.Content.activeSelf, Is.False);
                Assert.That(Handlers(promo, "Initialised"), Is.Empty);
                Assert.That(fixture.Icon.sprite, Is.Null);
                Assert.That(fixture.Resources.Log, Is.EqualTo(new[] { "ReleaseIconTexture(shown)" }));
            }
        }

        // OnEnable 0x1A17F00 with the system registered initialises at once; OnDisable 0x1A185C0
        // before any system only leaves OnSystemStartup.
        [Test]
        public void EnableWithARegisteredSystemInitialisesAndDisableWithoutOneLeavesStartup()
        {
            using (var fixture = new Fixture(typeof(CrossPromoLaunchButtonProbe)))
            {
                using (var row = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<CrossPromo>()))
                {
                    CrossPromo promo = NewCrossPromo(fixture.Resources);
                    row.Register(promo);
                    fixture.Invoke("OnEnable");
                    Assert.That(Get(fixture.Button, "m_crossPromo"), Is.SameAs(promo));
                    Assert.That(Handlers(Get(fixture.Button, "m_crossPromoRef"), "OnSystemStartup"), Is.Empty);
                    Assert.That(Handlers(promo, "Initialised").Single().Target, Is.SameAs(fixture.Button));
                    fixture.Invoke("OnDisable");
                    Assert.That(Handlers(promo, "Initialised"), Is.Empty);
                }

                var reference = new SystemRef<CrossPromo>("lucid-detached-crosspromo", null);
                Set(fixture.Button, "m_crossPromoRef", reference);
                Set(fixture.Button, "m_crossPromo", null);
                MethodInfo initialise = typeof(CrossPromoLaunchButton).GetMethod("InitialiseCrossPromo", BindingFlags.Instance | BindingFlags.NonPublic);
                Action<CrossPromo> other = system => { };
                reference.OnSystemStartup += other;
                reference.OnSystemStartup += (Action<CrossPromo>)Delegate.CreateDelegate(typeof(Action<CrossPromo>), fixture.Button, initialise);
                fixture.Content.SetActive(true);
                fixture.Resources.Log.Clear();
                fixture.Invoke("OnDisable");
                Assert.That(fixture.Content.activeSelf, Is.False);
                Assert.That(Handlers(reference, "OnSystemStartup"), Is.EqualTo(new Delegate[] { other }));
                Assert.That(fixture.Resources.Log, Is.Empty);
            }
        }
    }
}
