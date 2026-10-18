using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Hardlight.CrossPromo and Hardlight.CrossPromoDataParser checked against the 1.10.1 x86-64 bodies
    // cited per case. Nothing is downloaded: CrossPromo runs over recording collaborators and the parser
    // reads JSON held in memory. Every config asset is destroyed before the case continues, and with
    // Debug off no report reaches ProcessManager.
    public sealed class OriginalCrossPromoTests
    {
        private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Instance |
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private const string Router = "VersionRouter";

        // ---------------------------------------------------------------- CrossPromo

        // 0x1A0F570: DownloadCompleted is subscribed before DownloadMetaData is called, and the sync
        // is in progress from then on (IsReady 0x1A0E820 reads m_syncInProgress first).
        [Test]
        public void ConstructorSubscribesBeforeRequestingMetadataAndStartsTheSync()
        {
            var resources = new RecordingResourceManager();
            var promo = new CrossPromo(default(CrossPromoConfig), resources, new RecordingParser());

            Assert.That(resources.Calls, Is.EqualTo(new[] { "add_DownloadCompleted", "DownloadMetaData" }));
            Assert.That(resources.MetaDataCallback, Is.Not.Null);
            Assert.That(promo.IsReady(), Is.False);
            Assert.That(resources.Calls.Count, Is.EqualTo(2), "IsReady stops at m_syncInProgress");
        }

        // 0x1A0F7A0: empty metadata, a JSON null and a failed parse each end the sync without asking
        // for the ad images or raising Initialised; success is never read.
        [TestCase("", true, TestName = "EmptyMetadataEndsTheSyncWithoutInitialised")]
        [TestCase("null", true, TestName = "JsonNullEndsTheSyncWithoutInitialised")]
        [TestCase("{\"a\":{}}", false, TestName = "RejectedMetadataEndsTheSyncWithoutInitialised")]
        public void UnusableMetadataEndsTheSyncWithoutInitialised(string json, bool success)
        {
            var resources = new RecordingResourceManager { MetaData = json };
            var parser = new RecordingParser { Result = false };
            var promo = new CrossPromo(default(CrossPromoConfig), resources, parser);
            var raised = new List<CrossPromoMetaData>();
            ((ICrossPromo)promo).Initialised += raised.Add;

            resources.MetaDataCallback(success);

            Assert.That(raised, Is.Empty);
            Assert.That(resources.Calls, Has.No.Member("VerifyAdResources"));
            Assert.That(parser.Calls, Is.EqualTo(json == "{\"a\":{}}" ? 1 : 0));
            Assert.That(promo.IsReady(), Is.True);
        }

        // 0x1A0F9CD: parsed metadata whose system flag is off ends the sync like a failure.
        [Test]
        public void DisabledMetadataEndsTheSyncWithoutVerifyingAds()
        {
            var resources = new RecordingResourceManager { MetaData = "{\"root\":{\"k\":1}}" };
            var parser = new RecordingParser { Result = true, Parsed = new CrossPromoMetaData { IsSystemEnabled = false, Ads = new CrossPromoAd[0] } };
            var promo = new CrossPromo(default(CrossPromoConfig), resources, parser);
            var raised = new List<CrossPromoMetaData>();
            ((ICrossPromo)promo).Initialised += raised.Add;

            resources.MetaDataCallback(true);

            Assert.That(parser.LastRoot["root"]["k"], Is.EqualTo(1L), "JsonConvert hands the parser the whole document");
            Assert.That(resources.Calls, Has.No.Member("VerifyAdResources"));
            Assert.That(raised, Is.Empty);
            Assert.That(promo.IsReady(), Is.True);
            Assert.That(promo.IsEnabled, Is.False);
        }

        // 0x1A0FA4F then the inlined TryFinishSync (0x1A0FA6D): enabled metadata asks for its ads; the
        // sync finishes at once only when nothing is pending, otherwise on DownloadCompleted (0x1A0FC00),
        // which re-reads HasPendingDownloads and ignores its argument.
        [Test]
        public void EnabledMetadataVerifiesAdsAndFinishesWhenNoDownloadIsPending()
        {
            CrossPromoAd[] ads = { new CrossPromoAd { Name = "a" }, new CrossPromoAd { Name = "b" } };
            var parsed = new CrossPromoMetaData { IsSystemEnabled = true, Ads = ads, FirstAd = "b", PressToPlayString = "Play" };
            var resources = new RecordingResourceManager { MetaData = "{}", Pending = true };
            var promo = new CrossPromo(default(CrossPromoConfig), resources, new RecordingParser { Result = true, Parsed = parsed });
            var raised = new List<CrossPromoMetaData>();
            ((ICrossPromo)promo).Initialised += raised.Add;

            resources.MetaDataCallback(false);
            Assert.That(resources.VerifiedAds, Is.SameAs(ads));
            Assert.That(raised, Is.Empty);
            Assert.That(promo.IsReady(), Is.False);
            Assert.That(promo.IsEnabled, Is.True);

            resources.RaiseDownloadCompleted(true);
            Assert.That(raised, Is.Empty, "still pending");

            resources.Pending = false;
            resources.RaiseDownloadCompleted(false);
            Assert.That(raised.Count, Is.EqualTo(1));
            Assert.That(raised[0].Ads, Is.SameAs(ads));
            Assert.That(raised[0].FirstAd, Is.EqualTo("b"));
            Assert.That(promo.IsReady(), Is.True);

            // Each DownloadCompleted with nothing pending raises Initialised again.
            resources.RaiseDownloadCompleted(true);
            Assert.That(raised.Count, Is.EqualTo(2));
        }

        [Test]
        public void EnabledMetadataWithNothingPendingRaisesInitialisedAtOnce()
        {
            var parsed = new CrossPromoMetaData { IsSystemEnabled = true, Ads = new CrossPromoAd[0], FirstAd = "x" };
            var resources = new RecordingResourceManager { MetaData = "{}" };
            var promo = new CrossPromo(default(CrossPromoConfig), resources, new RecordingParser { Result = true, Parsed = parsed });
            var raised = new List<CrossPromoMetaData>();
            ((ICrossPromo)promo).Initialised += raised.Add;

            resources.MetaDataCallback(true);

            Assert.That(raised.Select(m => m.FirstAd), Is.EqualTo(new[] { "x" }));
            Assert.That(promo.IsReady(), Is.True);
        }

        // 0x1A0E6B0: once ready, a new subscriber is called at once with m_metaData and is not kept;
        // a null subscriber is ignored. Remove (0x1A0E8E0) only edits the private event.
        [Test]
        public void InitialisedAddCallsBackOnceReadyInsteadOfSubscribing()
        {
            var parsed = new CrossPromoMetaData { IsSystemEnabled = true, Ads = new CrossPromoAd[0], FirstAd = "late" };
            var resources = new RecordingResourceManager { MetaData = "{}" };
            var promo = new CrossPromo(default(CrossPromoConfig), resources, new RecordingParser { Result = true, Parsed = parsed });
            var early = new List<string>();
            var removed = new List<string>();
            Action<CrossPromoMetaData> removedHandler = m => removed.Add(m.FirstAd);
            ICrossPromo view = promo;
            view.Initialised += m => early.Add(m.FirstAd);
            view.Initialised += removedHandler;
            view.Initialised -= removedHandler;
            resources.MetaDataCallback(true);
            Assert.That(early, Is.EqualTo(new[] { "late" }));
            Assert.That(removed, Is.Empty);

            var late = new List<string>();
            view.Initialised += m => late.Add(m.FirstAd);
            Assert.That(late, Is.EqualTo(new[] { "late" }));
            Assert.DoesNotThrow(() => view.Initialised += null);

            resources.RaiseDownloadCompleted(true);
            Assert.That(early, Is.EqualTo(new[] { "late", "late" }));
            Assert.That(late, Is.EqualTo(new[] { "late" }), "the late subscriber was never stored");
        }

        // 0x1A0FCE0..0x1A0FEF0 forward to the resource manager; 0x1A0FFA0..0x1A10040 raise their own
        // events, RequestFullScreenView with the stored metadata; IsSupported (0x1A10060) is true.
        [Test]
        public void TextureCallsForwardAndRaisersReachOnlyTheirOwnEvent()
        {
            var resources = new RecordingResourceManager();
            var promo = new CrossPromo(default(CrossPromoConfig), resources, new RecordingParser());
            resources.Calls.Clear();
            var texture = new CrossPromoTexture { URL = "u" };

            Assert.That(promo.RequestIconTexture(texture), Is.Null);
            promo.ReleaseIconTexture(null);
            Assert.That(promo.RequestFullScreenTexture(texture), Is.Null);
            promo.ReleaseFullScreenTexture(null);
            Assert.That(resources.Calls, Is.EqualTo(new[] {
                "RequestIconTexture:u", "ReleaseIconTexture", "RequestFullScreenTexture:u", "ReleaseFullScreenTexture" }));

            var log = new List<string>();
            promo.LaunchButtonShown += (ad, count) => log.Add("shown " + ad.Name + " " + count);
            promo.FullScreenViewRequested += (meta, index) => log.Add("requested " + meta.FirstAd + " " + index);
            promo.FullScreenViewShown += ad => log.Add("impression " + ad.Name);
            promo.FullScreenButtonClicked += ad => log.Add("clicked " + ad.Name);
            promo.FullScreenViewClosed += () => log.Add("closed");
            promo.RaiseImpressionEvent(new CrossPromoAd { Name = "a" }, 3);
            promo.RequestFullScreenView(2);
            promo.RaiseFullScreenImpression(new CrossPromoAd { Name = "b" });
            promo.RaiseFullScreenButtonClicked(new CrossPromoAd { Name = "c" });
            promo.RaiseFullScreenClosedEvent();
            Assert.That(log, Is.EqualTo(new[] { "shown a 3", "requested  2", "impression b", "clicked c", "closed" }));
            Assert.That(CrossPromo.IsSupported(), Is.True);

            var silent = new CrossPromo(default(CrossPromoConfig), new RecordingResourceManager(), new RecordingParser());
            Assert.DoesNotThrow(() =>
            {
                silent.RaiseImpressionEvent(default, 0);
                silent.RequestFullScreenView(0);
                silent.RaiseFullScreenImpression(default);
                silent.RaiseFullScreenButtonClicked(default);
                silent.RaiseFullScreenClosedEvent();
            });
        }

        // DummyDll TypeDef 0x02000004: flags, attributes, field order and the 31 MethodDef rows
        // 0x06000003..0x06000021 in metadata order with their flags. The two-argument constructor
        // (0x1A0F040) is only inspected: running it would create folders and start a download.
        [Test]
        public void CrossPromoDeclarationsKeepOriginalFlagsAndOrder()
        {
            Assert.That((int)typeof(CrossPromo).Attributes, Is.EqualTo(0x00100001));
            Assert.That(typeof(CrossPromo).GetInterfaces(), Is.EquivalentTo(new[] { typeof(ICrossPromo), typeof(ISystem) }));
            Assert.That(OptionRows(typeof(CrossPromo)), Is.EqualTo(new[] { Option.ArrayBoundsChecks, Option.NullChecks }));
            Assert.That(Fields(typeof(CrossPromo)), Is.EqualTo(new[] {
                "Initialised 0x0001", "FullScreenViewRequested 0x0001", "LaunchButtonShown 0x0001", "FullScreenViewShown 0x0001",
                "FullScreenButtonClicked 0x0001", "FullScreenViewClosed 0x0001", "m_config 0x0021", "m_resourceManager 0x0021",
                "m_parser 0x0021", "m_syncInProgress 0x0001", "m_metaData 0x0001" }));

            MethodBase[] methods = Methods(typeof(CrossPromo));
            Assert.That(methods.Select(Signature), Is.EqualTo(new[] {
                "add_Initialised 0x0881", "remove_Initialised 0x0881",
                "Hardlight.ICrossPromo.add_Initialised 0x09e1", "Hardlight.ICrossPromo.remove_Initialised 0x09e1",
                "add_FullScreenViewRequested 0x09e6", "remove_FullScreenViewRequested 0x09e6",
                "add_LaunchButtonShown 0x09e6", "remove_LaunchButtonShown 0x09e6",
                "add_FullScreenViewShown 0x09e6", "remove_FullScreenViewShown 0x09e6",
                "add_FullScreenButtonClicked 0x09e6", "remove_FullScreenButtonClicked 0x09e6",
                "add_FullScreenViewClosed 0x09e6", "remove_FullScreenViewClosed 0x09e6",
                "get_IsEnabled 0x0886", "IsReady 0x01e6", ".ctor 0x1886", ".ctor 0x1886",
                "OnMetaDataDownloadCompleted 0x0081", "DownloadCompleted 0x0081", "TryFinishSync 0x0081",
                "RequestIconTexture 0x01e6", "ReleaseIconTexture 0x01e6", "RequestFullScreenTexture 0x01e6",
                "ReleaseFullScreenTexture 0x01e6", "RaiseImpressionEvent 0x01e6", "RequestFullScreenView 0x01e6",
                "RaiseFullScreenImpression 0x01e6", "RaiseFullScreenButtonClicked 0x01e6",
                "RaiseFullScreenClosedEvent 0x01e6", "IsSupported 0x0096" }));
            Assert.That(methods.OfType<ConstructorInfo>().Select(c => string.Join(",", c.GetParameters().Select(p => p.ParameterType.Name))),
                Is.EqualTo(new[] { "CrossPromoConfig,ICoroutineHandler", "CrossPromoConfig,ICrossPromoResourceManager,ICrossPromoDataParser" }));
            // The compiler-generated rows are exactly the field-like event accessors and their fields.
            Assert.That(methods.Where(m => m.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false))
                .Select(m => m.Name), Is.EqualTo(methods.Select(m => m.Name)
                .Where(n => (n.StartsWith("add_", StringComparison.Ordinal) || n.StartsWith("remove_", StringComparison.Ordinal)))));
            Assert.That(typeof(CrossPromo).GetFields(Declared)
                .Where(f => f.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false)).Count(), Is.EqualTo(6));
        }

        // ---------------------------------------------------------------- CrossPromoDataParser

        // 0x1A0F4A0 and TryFindProperty 0x1A106D0: "<name>-<platform>" wins over "<name>"; a blank
        // platform name adds no suffix, so only the bare key is read.
        [TestCase("ios", "suffixed", TestName = "PlatformSuffixedKeyWinsOverTheBareKey")]
        [TestCase(" ", "bare", TestName = "BlankPlatformReadsOnlyTheBareKey")]
        public void PlatformSuffixSelectsTheKey(string platform, string expected)
        {
            var parser = new CrossPromoDataParser(Config(platform));
            string json = Document("\"1.10.1\":\"meta\"",
                "\"meta\":{\"SystemEnabled\":\"1\",\"FirstAd\":\"bare\",\"FirstAd-ios\":\"suffixed\",\"FirstAd- \":\"spaced\",\"Ads\":[]}");

            Assert.That(Parse(parser, json, out CrossPromoMetaData metaData), Is.True);
            Assert.That(metaData.FirstAd, Is.EqualTo(expected));
            Assert.That(metaData.Ads, Is.Empty);
        }

        // 0x1A11140: a key equal to the app version is taken at once; otherwise the highest key below
        // it; keys above it are never chosen, and with none below the parse fails without a report.
        [TestCase("\"1.9\":\"low\",\"1.10.1\":\"exact\",\"1.10.2\":\"high\"", "exact", TestName = "VersionRouterTakesTheExactAppVersion")]
        [TestCase("\"1.2\":\"low\",\"1.10\":\"best\",\"1.10.2\":\"high\",\"1.9.9\":\"mid\"", "best", TestName = "VersionRouterTakesTheHighestVersionBelowTheApp")]
        [TestCase("\"2.0\":\"high\",\"1.11\":\"higher\"", null, TestName = "VersionRouterWithOnlyHigherVersionsFails")]
        public void VersionRouterPicksTheMetadataRoot(string router, string expectedRoot)
        {
            var parser = new CrossPromoDataParser(Config("ios"));
            var roots = new[] { "low", "exact", "high", "best", "mid", "higher" }
                .Select(name => "\"" + name + "\":{\"SystemEnabled\":\"1\",\"FirstAd\":\"" + name + "\",\"Ads\":[]}");
            string json = Document(router, roots.ToArray());

            bool parsed = Parse(parser, json, out CrossPromoMetaData metaData);

            Assert.That(parsed, Is.EqualTo(expectedRoot != null));
            Assert.That(metaData.FirstAd, Is.EqualTo(expectedRoot));
        }

        // 0x1A10A10: each missing step fails with metaData left at its default; a missing Ads array
        // fails after metaData is stored with no ads.
        [Test]
        public void MetadataFailuresStopAtTheFirstMissingStep()
        {
            var parser = new CrossPromoDataParser(Config("ios"));
            Assert.That(parser.TryParseMetadata(null, out CrossPromoMetaData metaData), Is.False);
            Assert.That(metaData, Is.EqualTo(default(CrossPromoMetaData)));

            Assert.That(Parse(parser, "{\"other\":{}}", out metaData), Is.False, "router");
            Assert.That(Parse(parser, Document("\"1.10.1\":\"absent\""), out metaData), Is.False, "meta root");
            Assert.That(metaData, Is.EqualTo(default(CrossPromoMetaData)));

            string noAds = Document("\"1.10.1\":\"meta\"", "\"meta\":{\"SystemEnabled\":\"1\",\"FirstAd\":\"f\"}", PressToPlay("\"en\":\"Go\""));
            Assert.That(Parse(parser, noAds, out metaData), Is.False, "Ads array");
            Assert.That(metaData.IsSystemEnabled, Is.True);
            Assert.That(metaData.FirstAd, Is.EqualTo("f"));
            Assert.That(metaData.PressToPlayString, Is.EqualTo("Go"));
            Assert.That(metaData.Ads, Is.Null);
        }

        // GetPropertyAsBool 0x1A107D0: only "1" is true. FindPressToPlayString 0x1A11310: the config
        // language, then "default" when that text is missing or empty; null without the table.
        [TestCase("\"1\"", "en", "\"en\":\"Play\",\"default\":\"Def\"", true, "Play", TestName = "SystemFlagOneAndLanguageTextAreRead")]
        [TestCase("\"true\"", "en", "\"en\":\"\",\"default\":\"Def\"", false, "Def", TestName = "EmptyLanguageTextFallsBackToDefault")]
        [TestCase("1", "fr", "\"default\":\"Def\"", true, "Def", TestName = "MissingLanguageTextFallsBackToDefault")]
        [TestCase("\"\"", null, "\"default\":\"Def\"", false, "Def", TestName = "NullLanguageReadsDefault")]
        [TestCase("\"0\"", "en", null, false, null, TestName = "MissingPressToPlayTableGivesNull")]
        public void SystemFlagAndPressToPlayText(string flag, string language, string strings, bool enabled, string text)
        {
            var parser = new CrossPromoDataParser(Config("ios", language: language));
            string meta = "\"meta\":{\"SystemEnabled\":" + flag + ",\"Ads\":[]}";
            string json = strings == null ? Document("\"1.10.1\":\"meta\"", meta) : Document("\"1.10.1\":\"meta\"", meta, PressToPlay(strings));

            Assert.That(Parse(parser, json, out CrossPromoMetaData metaData), Is.True);
            Assert.That(metaData.IsSystemEnabled, Is.EqualTo(enabled));
            Assert.That(metaData.PressToPlayString, Is.EqualTo(text));
        }

        // TryParseAdData 0x1A11490 and TryParseTexture 0x1A11810: ads listed in order; a missing,
        // disabled or incomplete ad is dropped and the rest are kept.
        [Test]
        public void AdsKeepListOrderAndDropMissingDisabledOrIncompleteEntries()
        {
            RequireScreen(out int width, out int height);
            var parser = new CrossPromoDataParser(Config("ios"));
            string image = Image("main", width, height);
            string json = Document("\"1.10.1\":\"meta\"",
                "\"meta\":{\"SystemEnabled\":\"1\",\"Ads\":[\"second\",\"absent\",\"off\",\"nostore\",\"noicon\",\"badicon\",\"nourl\",\"nomain\",\"emptymain\",\"badmain\",\"first\"]}",
                Ad("second", "\"1\"", "\"s2\"", Image("icon2", 64, 32), "[" + image + "]"),
                Ad("off", "\"0\"", "\"s\"", Image("i", 1, 1), "[" + image + "]"),
                Ad("nostore", "\"1\"", "\"\"", Image("i", 1, 1), "[" + image + "]"),
                "\"noicon\":{\"enabled\":\"1\",\"storelink\":\"s\",\"mainimages\":[" + image + "]}",
                Ad("badicon", "\"1\"", "\"s\"", Image("i", 0, 5), "[" + image + "]"),
                Ad("nourl", "\"1\"", "\"s\"", "{\"width\":4,\"height\":4}", "[" + image + "]"),
                "\"nomain\":{\"enabled\":\"1\",\"storelink\":\"s\",\"iconimage\":" + Image("i", 1, 1) + "}",
                Ad("emptymain", "\"1\"", "\"s\"", Image("i", 1, 1), "[]"),
                Ad("badmain", "\"1\"", "\"s\"", Image("i", 1, 1), "[" + Image("m", 5, 0) + "]"),
                Ad("first", "\"1\"", "\"s1\"", Image("icon1", 8, 16), "[" + image + "]"));

            Assert.That(Parse(parser, json, out CrossPromoMetaData metaData), Is.True);
            Assert.That(metaData.Ads.Select(a => a.Name), Is.EqualTo(new[] { "second", "first" }));
            CrossPromoAd second = metaData.Ads[0];
            Assert.That(second.StoreURL, Is.EqualTo("s2"));
            Assert.That(second.IconTexture.URL, Is.EqualTo("icon2"));
            Assert.That(second.IconTexture.Resolution, Is.EqualTo(new Vector2Int(64, 32)));
            Assert.That(second.IconTexture.LocalFileName, Is.EqualTo(HashUtils.GetFileSafeSha1("icon2")));
            Assert.That(second.FullScreenTexture.URL, Is.EqualTo("main"));
            Assert.That(metaData.Ads[1].IconTexture.Resolution, Is.EqualTo(new Vector2Int(8, 16)));
        }

        // TryParseMainImage 0x1A11A90: an image exactly the screen size returns at once even after an
        // image of the same ratio; unparsable images are skipped.
        [Test]
        public void MainImageOfExactlyTheScreenSizeWins()
        {
            RequireScreen(out int width, out int height);
            string images = "[" + Image("broken", 0, height) + "," + Image("double", width * 2, height * 2) + "," +
                Image("exact", width, height) + "," + Image("later", width * 3, height * 3) + "]";
            Assert.That(ParseMain(images).URL, Is.EqualTo("exact"));
        }

        // Without an exact match the first parsed image is kept, a later one replaces it when its
        // rounded ratio is strictly closer to the screen's, and among equal ratios when its height is
        // strictly closer to the screen height.
        [Test]
        public void MainImagePrefersTheClosestRatioThenTheClosestHeight()
        {
            RequireScreen(out int width, out int height);
            Assert.That(ParseMain("[" + Image("far", width, height * 4) + "," + Image("near", width * 4, height * 4) + "]").URL,
                Is.EqualTo("near"));
            Assert.That(ParseMain("[" + Image("near", width * 4, height * 4) + "," + Image("far", width, height * 4) + "]").URL,
                Is.EqualTo("near"));
            Assert.That(ParseMain("[" + Image("tall", width * 4, height * 4) + "," + Image("closer", width * 2, height * 2) + "," +
                Image("further", width * 3, height * 3) + "]").URL, Is.EqualTo("closer"));
            Assert.That(ParseMain("[" + Image("only", width, height * 4) + "]").URL, Is.EqualTo("only"));
        }

        // DummyDll TypeDef 0x02000008: flags, attributes, the literal PlatformSuffix and the 14
        // MethodDef rows 0x0600003e..0x0600004b with their flags and parameter defaults.
        [Test]
        public void ParserDeclarationsKeepOriginalFlagsOrderAndDefaults()
        {
            Assert.That((int)typeof(CrossPromoDataParser).Attributes, Is.EqualTo(0x00100001));
            Assert.That(typeof(CrossPromoDataParser).GetInterfaces(), Is.EqualTo(new[] { typeof(ICrossPromoDataParser) }));
            Assert.That(OptionRows(typeof(CrossPromoDataParser)), Is.EqualTo(new[] { Option.NullChecks, Option.ArrayBoundsChecks }));
            Assert.That(Fields(typeof(CrossPromoDataParser)), Is.EqualTo(new[] { "PlatformSuffix 0x8051", "m_config 0x0021", "m_platformName 0x0021" }));
            Assert.That(typeof(CrossPromoDataParser).GetField("PlatformSuffix", Declared).GetRawConstantValue(), Is.EqualTo(""));

            MethodBase[] methods = Methods(typeof(CrossPromoDataParser));
            Assert.That(methods.Select(Signature), Is.EqualTo(new[] {
                ".ctor 0x1886", "TryFindProperty 0x0081", "GetPropertyAsBool 0x0081", "GetPropertyAsInt 0x0081",
                "GetPropertyAsString 0x0081", "GetPropertyAs 0x0081", "GetPropertyAsObjList 0x0081", "GetPropertyAsDic 0x0081",
                "TryParseMetadata 0x01e6", "ParseVersionRouter 0x0081", "TryParseAdData 0x0081", "TryParseTexture 0x0081",
                "TryParseMainImage 0x0081", "FindPressToPlayString 0x0081" }));
            Assert.That(methods[2].GetParameters()[2].Attributes, Is.EqualTo(ParameterAttributes.Optional | ParameterAttributes.HasDefault));
            Assert.That(methods[2].GetParameters()[2].DefaultValue, Is.EqualTo(false));
            Assert.That(methods[3].GetParameters()[2].DefaultValue, Is.EqualTo(0));
            Assert.That(methods[5].GetGenericArguments().Select(a => a.Name + " " + (int)a.GenericParameterAttributes), Is.EqualTo(new[] { "TValue 0" }));
            Assert.That(new[] { 1, 8, 10, 11, 12 }.Select(i => methods[i].GetParameters().Last().Attributes),
                Is.All.EqualTo(ParameterAttributes.Out));
            Assert.That(methods.SelectMany(m => m.GetCustomAttributesData()), Is.Empty);
        }

        // ---------------------------------------------------------------- helpers

        private static CrossPromoTexture ParseMain(string images)
        {
            var parser = new CrossPromoDataParser(Config("ios"));
            string json = Document("\"1.10.1\":\"meta\"", "\"meta\":{\"SystemEnabled\":\"1\",\"Ads\":[\"ad\"]}",
                Ad("ad", "\"1\"", "\"s\"", Image("icon", 1, 1), images));
            Assert.That(Parse(parser, json, out CrossPromoMetaData metaData), Is.True);
            Assert.That(metaData.Ads.Length, Is.EqualTo(1), "the ad needs a main image");
            return metaData.Ads[0].FullScreenTexture;
        }

        private static void RequireScreen(out int width, out int height)
        {
            width = Screen.width;
            height = Screen.height;
            Assert.Greater(width, 0, "Main image selection needs a positive Screen size");
            Assert.Greater(height, 0, "Main image selection needs a positive Screen size");
        }

        // Parses through CrossPromo's own JsonConvert call (0x1A0F8F0), as the shipped code does; the
        // test assembly has no Newtonsoft reference of its own.
        private static bool Parse(CrossPromoDataParser parser, string json, out CrossPromoMetaData metaData)
        {
            var resources = new RecordingResourceManager { MetaData = json };
            var capture = new RecordingParser { Inner = parser };
            new CrossPromo(default(CrossPromoConfig), resources, capture);
            resources.MetaDataCallback(true);
            Assert.That(capture.Calls, Is.EqualTo(1), "the JSON reached the parser");
            metaData = capture.Parsed;
            return capture.Result;
        }

        private static string Document(string router, params string[] roots) =>
            "{\"" + Router + "\":{" + router + "}" + string.Concat(roots.Select(r => "," + r)) + "}";

        private static string PressToPlay(string strings) => "\"PressToPlayString\":{" + strings + "}";

        private static string Ad(string name, string enabled, string store, string icon, string mainImages) =>
            "\"" + name + "\":{\"enabled\":" + enabled + ",\"storelink\":" + store + ",\"iconimage\":" + icon + ",\"mainimages\":" + mainImages + "}";

        private static string Image(string url, int width, int height) =>
            "{\"url\":\"" + url + "\",\"width\":" + width + ",\"height\":" + height + "}";

        // Builds the config through CrossPromoConfigurationAsset; the asset is destroyed at once and
        // persistentDataPath is only joined into paths, never touched.
        private static CrossPromoConfig Config(string platform, string appVersion = "1.10.1", string language = "en")
        {
            var asset = ScriptableObject.CreateInstance<CrossPromoConfigurationAsset>();
            try
            {
                using (var serialized = new SerializedObject(asset))
                {
                    serialized.FindProperty("m_routerPropertyName").stringValue = Router;
                    serialized.FindProperty("m_platformName").stringValue = platform;
                    Assert.That(serialized.ApplyModifiedPropertiesWithoutUndo(), Is.True);
                }
                CrossPromoConfig config = new CrossPromoConfig(asset, appVersion, "GB", language, "unused");
                Assert.That(config.Debug, Is.False);
                return config;
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        private static MethodBase[] Methods(Type type) =>
            type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared))
                .OrderBy(m => m.MetadataToken).ToArray();

        private static string Signature(MethodBase method) => method.Name + " 0x" + ((int)method.Attributes).ToString("x4");

        private static string[] Fields(Type type) =>
            type.GetFields(Declared).OrderBy(f => f.MetadataToken).Select(f => f.Name + " 0x" + ((int)f.Attributes).ToString("x4")).ToArray();

        // Il2CppSetOption rows in metadata order; every value is false.
        private static Option[] OptionRows(Type type)
        {
            Assert.That(type.GetCustomAttributes<Il2CppSetOptionAttribute>().Select(a => a.Value), Is.All.EqualTo(false));
            return type.GetCustomAttributesData().Where(a => a.AttributeType == typeof(Il2CppSetOptionAttribute))
                .Select(a => (Option)a.ConstructorArguments[0].Value).ToArray();
        }

        // Records calls; MetaData is what RequestMetaData returns and Pending what HasPendingDownloads reads.
        private sealed class RecordingResourceManager : ICrossPromoResourceManager
        {
            public readonly List<string> Calls = new List<string>();
            public string MetaData;
            public bool Pending;
            public Action<bool> MetaDataCallback;
            public CrossPromoAd[] VerifiedAds;
            private Action<bool> m_downloadCompleted;

            public event Action<bool> DownloadCompleted
            {
                add { Calls.Add("add_DownloadCompleted"); m_downloadCompleted += value; }
                remove { Calls.Add("remove_DownloadCompleted"); m_downloadCompleted -= value; }
            }

            public bool HasPendingDownloads
            {
                get { Calls.Add("HasPendingDownloads"); return Pending; }
            }

            public void RaiseDownloadCompleted(bool success) => m_downloadCompleted(success);

            public void DownloadMetaData(Action<bool> onMetaDataDownloadCompleted)
            {
                Calls.Add("DownloadMetaData");
                MetaDataCallback = onMetaDataDownloadCompleted;
            }

            public void VerifyAdResources(CrossPromoAd[] metaDataAds)
            {
                Calls.Add("VerifyAdResources");
                VerifiedAds = metaDataAds;
            }

            public Sprite RequestFullScreenTexture(CrossPromoTexture texMeta)
            {
                Calls.Add("RequestFullScreenTexture:" + texMeta.URL);
                return null;
            }

            public void ReleaseFullScreenTexture(Sprite texture) => Calls.Add("ReleaseFullScreenTexture");

            public Sprite RequestIconTexture(CrossPromoTexture texMeta)
            {
                Calls.Add("RequestIconTexture:" + texMeta.URL);
                return null;
            }

            public void ReleaseIconTexture(Sprite texture) => Calls.Add("ReleaseIconTexture");

            public string RequestMetaData()
            {
                Calls.Add("RequestMetaData");
                return MetaData;
            }
        }

        private sealed class RecordingParser : ICrossPromoDataParser
        {
            public bool Result;
            public CrossPromoMetaData Parsed;
            public int Calls;
            public Dictionary<string, Dictionary<string, object>> LastRoot;

            // When set, the real parser answers and its result and metadata are recorded.
            public CrossPromoDataParser Inner;

            public bool TryParseMetadata(Dictionary<string, Dictionary<string, object>> dataRoot, out CrossPromoMetaData metaData)
            {
                Calls++;
                LastRoot = dataRoot;
                if (Inner != null)
                {
                    Result = Inner.TryParseMetadata(dataRoot, out Parsed);
                }
                metaData = Parsed;
                return Result;
            }
        }
    }
}
