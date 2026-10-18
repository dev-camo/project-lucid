using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // HLCrossPromo.Runtime value types, HashUtils, the shared resource pool and CrossPromoPage, checked
    // against the 1.10.1 x86-64 bodies cited per case. Inputs are in-memory only: no endpoint is
    // contacted, no file is touched and no ProcessManager row is used. Every Unity object a case
    // creates is destroyed by the case; the page probe stays on an inactive GameObject so no Graphic
    // registers with the canvas update registry.
    public sealed class OriginalCrossPromoCoreTests
    {
        private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Instance |
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        // 0x1A15DD0: SHA1.Create().ComputeHash(UTF8.GetBytes(value)), Convert.ToBase64String, then
        // String.Replace("/", string.Empty). Expected strings are SHA-1/Base64 computed outside Unity.
        [Test]
        public void FileSafeSha1RemovesEverySlashAndKeepsPlusAndPadding()
        {
            Assert.That(HashUtils.GetFileSafeSha1("https://cdn.example.invalid/icon_27.png"),
                Is.EqualTo("n5T04mN7zi0MhHo3eJ+vo9lQM="), "raw n5T04mN/7zi0MhH/o3eJ+vo9lQM=");
            Assert.That(HashUtils.GetFileSafeSha1(""), Is.EqualTo("2jmj7l5rSw0yVbvlWAYkKYBwk="),
                "raw 2jmj7l5rSw0yVb/vlWAYkK/YBwk=");
            // UTF-8 rather than UTF-16 or Latin-1 bytes: raw exeNmuXuyAK/L3LNaaBhlUh9sLk=.
            Assert.That(HashUtils.GetFileSafeSha1("Sonic é"), Is.EqualTo("exeNmuXuyAKL3LNaaBhlUh9sLk="));
            // Encoding.GetBytes(string) (vtable slot 17) rejects null before any hashing.
            Assert.Throws<ArgumentNullException>(() => HashUtils.GetFileSafeSha1(null));
        }

        // 0x1A11F80: URL, then Resolution, then LocalFileName = GetFileSafeSha1(url).
        [Test]
        public void TextureConstructorDerivesLocalFileNameFromUrlOnly()
        {
            var first = new CrossPromoTexture("https://cdn.example.invalid/icon_27.png", new Vector2Int(512, 256));
            Assert.That(first.URL, Is.EqualTo("https://cdn.example.invalid/icon_27.png"));
            Assert.That(first.Resolution, Is.EqualTo(new Vector2Int(512, 256)));
            Assert.That(first.LocalFileName, Is.EqualTo("n5T04mN7zi0MhHo3eJ+vo9lQM="));
            var resized = new CrossPromoTexture("https://cdn.example.invalid/icon_27.png", new Vector2Int(-1, 0));
            Assert.That(resized.LocalFileName, Is.EqualTo(first.LocalFileName));
            Assert.Throws<ArgumentNullException>(() => new CrossPromoTexture(null, Vector2Int.zero));
        }

        // 0x1A15C70: String.Equals(Name, other.Name, Ordinal); 0x1A15C00: exact CrossPromoAd box,
        // then the same comparison; 0x1A15C90: Name?.GetHashCode() (vtable slot 2), else 0.
        [Test]
        public void AdIdentityIsTheOrdinalNameAlone()
        {
            var texture = new CrossPromoTexture("a", new Vector2Int(1, 1));
            var ad = new CrossPromoAd { Name = "Sonic", IconTexture = texture, StoreURL = "store-one" };
            var sameName = new CrossPromoAd { Name = "Sonic", FullScreenTexture = texture, StoreURL = "store-two" };
            var otherCase = new CrossPromoAd { Name = "SONIC" };
            var unnamed = new CrossPromoAd();
            Assert.That(ad.Equals(sameName), Is.True);
            Assert.That(ad.Equals(otherCase), Is.False);
            Assert.That(ad.Equals(unnamed), Is.False);
            Assert.That(unnamed.Equals(new CrossPromoAd()), Is.True);
            Assert.That(ad.Equals((object)sameName), Is.True);
            Assert.That(ad.Equals((object)otherCase), Is.False);
            Assert.That(ad.Equals((object)"Sonic"), Is.False);
            Assert.That(ad.Equals((object)null), Is.False);
            Assert.That(ad.GetHashCode(), Is.EqualTo("Sonic".GetHashCode()));
            Assert.That(unnamed.GetHashCode(), Is.Zero);
        }

        // 0x1A15CC0: null Ads returns 0; otherwise ReadOnlyListExtensions.FindIndex with
        // FindFirstAdIndex (0x1A15DA0: String.Compare(ad.Name, FirstAd, Ordinal) == 0) and cmovg keeps
        // only a positive index. The miss row separates this from returning the raw index (-1).
        [Test]
        public void FirstAdIndexClampsMissesAndMissingListsToZero()
        {
            CrossPromoAd[] ads = { Ad("one"), Ad(null), Ad("three"), Ad("three") };
            Assert.That(Meta(null, "one").GetFirstAdIndex(), Is.Zero);
            Assert.That(Meta(new CrossPromoAd[0], "one").GetFirstAdIndex(), Is.Zero);
            Assert.That(Meta(ads, "missing").GetFirstAdIndex(), Is.Zero);
            Assert.That(Meta(ads, "one").GetFirstAdIndex(), Is.Zero);
            Assert.That(Meta(ads, "three").GetFirstAdIndex(), Is.EqualTo(2));
            Assert.That(Meta(ads, "THREE").GetFirstAdIndex(), Is.Zero);
            Assert.That(Meta(ads, null).GetFirstAdIndex(), Is.EqualTo(1));
        }

        // 0x1A10130 reads the asset (null faults first), selects the Dev URL only for m_server == 0
        // and combines persistentDataPath with each folder (Path.Combine 0x06002785/0x0600279a);
        // 0x1A10300 builds "server?title=…&version=…&country=…" from a 7-element string array, parses
        // the version and stores the language without adding it to the URL.
        [Test]
        public void ConfigCopiesAssetValuesAndBuildsTheMetadataRequest()
        {
            var asset = ScriptableObject.CreateInstance<CrossPromoConfigurationAsset>();
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                Author(asset, 1, "Dream Team", "router", "macos", "in", "out", "meta.json", true, false, 5, 7, 4096L);
                var config = new CrossPromoConfig(asset, "1.10.1", "GB", "fr", "/persist");
                char separator = Path.DirectorySeparatorChar;
                Assert.That(config.MetaDataURL,
                    Is.EqualTo("https://gridserver.hardlightgames.com/info?title=Dream Team&version=1.10.1&country=GB"));
                Assert.That(config.AppVersion, Is.EqualTo(new Version(1, 10, 1)));
                Assert.That(config.LanguageId, Is.EqualTo("fr"));
                Assert.That(config.RouterPropertyName, Is.EqualTo("router"));
                Assert.That(config.PlatformName, Is.EqualTo("macos"));
                Assert.That(config.IncomingPath, Is.EqualTo("/persist" + separator + "in"));
                Assert.That(config.DestinationPath, Is.EqualTo("/persist" + separator + "out"));
                Assert.That(config.MetaFilePath, Is.EqualTo("/persist" + separator + "out" + separator + "meta.json"));
                Assert.That(config.Debug, Is.True);
                Assert.That(config.CanUseLocalStorage, Is.False);
                Assert.That(config.IconImagePoolSize, Is.EqualTo(5));
                Assert.That(config.MainImagePoolSize, Is.EqualTo(7));
                Assert.That(config.InvalidContentLengthThreshold, Is.EqualTo(4096L));

                // A null country concatenates as empty; Dev is selected only by m_server == 0.
                Author(asset, 1, "", "", "", "in", "out", "meta.json", false, true, 8, 3, 1024L);
                Assert.That(new CrossPromoConfig(asset, "2.0", null, null, "/p").MetaDataURL,
                    Is.EqualTo("https://gridserver.hardlightgames.com/info?title=&version=2.0&country="));
                Author(asset, 0, "t", "", "", "in", "out", "meta.json", false, true, 8, 3, 1024L);
                Assert.That(new CrossPromoConfig(asset, "3.4.5.6", "US", "en", "/p").MetaDataURL,
                    Is.EqualTo("https://gridserver-dev.hardlightdev.com/info?title=t&version=3.4.5.6&country=US"));

                Assert.Throws<ArgumentException>(() => new CrossPromoConfig(asset, "not-a-version", "US", "en", "/p"));
                Assert.Throws<NullReferenceException>(() => new CrossPromoConfig(null, "1.0", "US", "en", "/p"));
            }, () => Object.DestroyImmediate(asset));
        }

        // 0xBE4130/0xBE4170/0xBE4180: the count starts at 1, Get increments, Release decrements and
        // returns count <= 0 (setle). The third Release separates "<= 0" from "== 0".
        [Test]
        public void ReferenceCountsHoldersAndKeepsReportingReleasedBelowZero()
        {
            var resource = new object();
            var reference = new SharedResourceReference<object>(resource);
            Assert.That(reference.Get(), Is.SameAs(resource));
            Assert.That(reference.Release(), Is.False);
            Assert.That(reference.Release(), Is.True);
            Assert.That(reference.Release(), Is.True);
            // 0xBE41A0: m_resource.Equals(other), the virtual Object.Equals (vtable slot 0).
            var text = new SharedResourceReference<string>("abc");
            Assert.That(text.Equals(new string(new[] { 'a', 'b', 'c' })), Is.True);
            Assert.That(text.Equals("abd"), Is.False);
        }

        // 0xBE32D0 stores a fresh reference (count 1) through the indexer; 0xBE3360 clears the out value
        // and adds a holder on a hit; 0xBE33D0 ignores null, finds the key by value equality (0xBE3480),
        // and on the last release invokes the callback before Dictionary.Remove.
        [Test]
        public void PoolReleasesOnTheLastHolderAndCallsBackBeforeRemoval()
        {
            var released = new List<string>();
            SharedResourcePool<string, string> pool = null;
            bool presentDuringCallback = false;
            pool = new SharedResourcePool<string, string>(2, resource =>
            {
                released.Add(resource);
                presentDuringCallback = pool.TryGet("key", out _);
            });
            Assert.That(pool.TryGet("key", out string missing), Is.False);
            Assert.That(missing, Is.Null);

            pool.Add("key", "abc");
            Assert.That(pool.TryGet("key", out string hit), Is.True);
            Assert.That(hit, Is.EqualTo("abc"));
            pool.ReleaseResource(new string(new[] { 'a', 'b', 'c' }));
            Assert.That(released, Is.Empty, "Add plus one TryGet leave two holders");
            pool.ReleaseResource(null);
            pool.ReleaseResource("not-pooled");
            Assert.That(released, Is.Empty);
            pool.ReleaseResource("abc");
            Assert.That(released, Is.EqualTo(new[] { "abc" }));
            Assert.That(presentDuringCallback, Is.True);
            Assert.That(pool.TryGet("key", out _), Is.False);

            // Add replaces the stored reference, so the earlier holder count and resource are gone.
            pool.Add("key", "first");
            Assert.That(pool.TryGet("key", out _), Is.True);
            pool.Add("key", "second");
            pool.ReleaseResource("first");
            Assert.That(released, Is.EqualTo(new[] { "abc" }));
            pool.ReleaseResource("second");
            Assert.That(released, Is.EqualTo(new[] { "abc", "second" }));
        }

        // The callback defaults to null (no Constant row) and the null-conditional invoke skips it.
        [Test]
        public void PoolWithoutCallbackStillRemovesReleasedEntries()
        {
            var pool = new SharedResourcePool<int, object>();
            var resource = new object();
            pool.Add(4, resource);
            pool.ReleaseResource(resource);
            Assert.That(pool.TryGet(4, out object value), Is.False);
            Assert.That(value, Is.Null);
        }

        // 0x1A18950/0x1A18960 go through Image.sprite (GetSprite reads Image.m_Sprite), 0x1A18980 sets
        // Text.text (vtable slot 75), 0x1A189A0/0x1A189B0 store and return m_isReady.
        [Test]
        public void PageForwardsSpriteAndTextToItsComponents()
        {
            var texture = new Texture2D(2, 2);
            Sprite first = null, second = null;
            var owner = new GameObject("CrossPromoPageProbe");
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                owner.SetActive(false);
                first = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
                second = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
                var image = owner.AddComponent<Image>();
                var page = owner.AddComponent<CrossPromoPage>();
                page.Image = image;
                var textOwner = new GameObject("CrossPromoPageText");
                textOwner.transform.SetParent(owner.transform, false);
                page.ButtonTextField = textOwner.AddComponent<Text>();

                Assert.That(page.GetReadyState(), Is.False);
                page.SetSprite(first);
                Assert.That(page.Image.sprite, Is.SameAs(first));
                page.Image.sprite = second;
                Assert.That(page.GetSprite(), Is.SameAs(second));
                page.SetText("Press to play");
                Assert.That(page.ButtonTextField.text, Is.EqualTo("Press to play"));
                page.SetReadyState(true);
                Assert.That(page.GetReadyState(), Is.True);
                page.SetReadyState(false);
                Assert.That(page.GetReadyState(), Is.False);
            },
            () => Object.DestroyImmediate(owner),
            () => { if (first != null) Object.DestroyImmediate(first); },
            () => { if (second != null) Object.DestroyImmediate(second); },
            () => Object.DestroyImmediate(texture));
        }

        // DummyDll TypeDef flags, field order and the parameter defaults that dump.cs omits.
        [Test]
        public void DeclarationsKeepOriginalFlagsFieldOrderAndDefaults()
        {
            Assert.That((int)typeof(CrossPromoConfig).Attributes, Is.EqualTo(0x00100109));
            Assert.That((int)typeof(CrossPromoAd).Attributes, Is.EqualTo(0x00100109));
            Assert.That((int)typeof(CrossPromoMetaData).Attributes, Is.EqualTo(0x00100109));
            Assert.That((int)typeof(CrossPromoTexture).Attributes, Is.EqualTo(0x00100109));
            Assert.That((int)typeof(HashUtils).Attributes, Is.EqualTo(0x00100001));
            Assert.That((int)typeof(CrossPromoPage).Attributes, Is.EqualTo(0x00102001));
            Assert.That((int)typeof(SharedResourcePool<,>).Attributes, Is.EqualTo(0x00100001));
            Assert.That((int)typeof(SharedResourceReference<>).Attributes, Is.EqualTo(0x00100001));
            foreach (Type type in new[] { typeof(HashUtils), typeof(CrossPromoPage), typeof(SharedResourcePool<,>), typeof(SharedResourceReference<>) })
                Assert.That(type.GetCustomAttributes<Il2CppSetOptionAttribute>().Count(), Is.EqualTo(2), type.Name);

            Assert.That(Fields(typeof(CrossPromoConfig)), Is.EqualTo(new[] {
                "<RouterPropertyName>k__BackingField", "<AppVersion>k__BackingField", "<LanguageId>k__BackingField",
                "<MetaDataURL>k__BackingField", "<PlatformName>k__BackingField", "<IncomingPath>k__BackingField",
                "<DestinationPath>k__BackingField", "<MetaFilePath>k__BackingField", "<IconImagePoolSize>k__BackingField",
                "<MainImagePoolSize>k__BackingField", "<InvalidContentLengthThreshold>k__BackingField", "Debug",
                "<CanUseLocalStorage>k__BackingField" }));
            Assert.That(Fields(typeof(CrossPromoAd)), Is.EqualTo(new[] { "Name", "IconTexture", "FullScreenTexture", "StoreURL" }));
            Assert.That(Fields(typeof(CrossPromoMetaData)), Is.EqualTo(new[] { "IsSystemEnabled", "Ads", "FirstAd", "PressToPlayString" }));
            Assert.That(Fields(typeof(CrossPromoTexture)), Is.EqualTo(new[] { "URL", "Resolution", "LocalFileName" }));
            Assert.That(Fields(typeof(CrossPromoPage)), Is.EqualTo(new[] { "Image", "ButtonTextField", "m_isReady" }));
            Assert.That(Fields(typeof(SharedResourcePool<,>)), Is.EqualTo(new[] { "m_onReleasedCallback", "m_pool" }));
            Assert.That(Fields(typeof(SharedResourceReference<>)), Is.EqualTo(new[] { "m_resource", "m_refCount" }));

            ConstructorInfo hidden = typeof(CrossPromoConfig).GetConstructors(Declared).Single(c => c.IsPrivate);
            Assert.That(hidden.GetParameters().Skip(10).Select(p => p.DefaultValue),
                Is.EqualTo(new object[] { false, true, 8, 3, 1024L }));
            ParameterInfo[] pool = typeof(SharedResourcePool<,>).GetConstructors(Declared).Single().GetParameters();
            Assert.That(pool.Select(p => p.DefaultValue), Is.EqualTo(new object[] { 0, null }));
        }

        private static string[] Fields(Type type) =>
            type.GetFields(Declared).OrderBy(f => f.MetadataToken).Select(f => f.Name).ToArray();

        private static CrossPromoAd Ad(string name) => new CrossPromoAd { Name = name };

        private static CrossPromoMetaData Meta(CrossPromoAd[] ads, string firstAd) =>
            new CrossPromoMetaData { IsSystemEnabled = true, Ads = ads, FirstAd = firstAd };

        private static void Author(CrossPromoConfigurationAsset asset, int server, string title, string router, string platform,
            string incoming, string destination, string meta, bool debug, bool localStorage, int iconPool, int mainPool, long threshold)
        {
            using (var serialized = new SerializedObject(asset))
            {
                serialized.FindProperty("m_server").intValue = server;
                serialized.FindProperty("m_gameTitle").stringValue = title;
                serialized.FindProperty("m_routerPropertyName").stringValue = router;
                serialized.FindProperty("m_platformName").stringValue = platform;
                serialized.FindProperty("m_incomingFolder").stringValue = incoming;
                serialized.FindProperty("m_destinationFolder").stringValue = destination;
                serialized.FindProperty("m_metaFilename").stringValue = meta;
                serialized.FindProperty("m_debug").boolValue = debug;
                serialized.FindProperty("m_canUseLocalStorage").boolValue = localStorage;
                serialized.FindProperty("m_iconImagePoolSize").intValue = iconPool;
                serialized.FindProperty("m_mainImagePoolSize").intValue = mainPool;
                serialized.FindProperty("m_invalidContentLengthThreshold").longValue = threshold;
                Assert.That(serialized.ApplyModifiedPropertiesWithoutUndo(), Is.True);
            }
        }
    }
}
