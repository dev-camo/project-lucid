using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;
using Hardlight.Utils;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using Country = Hardlight.CountryRegulationIndexAPI.Country;

namespace ProjectLucid.Tests
{
    // Native-grounded cases for HLUnityCore LegalModule (0x0200014d) and
    // CountryRegulationIndexAPI (0x02000167), 1.10.1 x86-64. Modules are built
    // through the protected constructor only; no Initialise, file or coroutine
    // work runs. The constructor's CoroutineUtils registry row is owned or
    // borrowed by the fixture and restored afterwards. Flow iterators are
    // driven by MoveNext directly; RequestAccess is given an empty coroutine
    // host so it faults at the host fetch, after its field writes. The
    // HLUnityCore tracking bridge static is swapped and restored per case.
    [TestFixture]
    public sealed class OriginalLegalModuleTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

        // Country.Equals 0x060009d3 (RVA 0x1AF8100): id goes through string ==,
        // type through the instance string.Equals (null type faults at 0x1AF81FE).
        // op_Equality 0x060009d1 short-circuits a null left side by reference.
        [Test]
        public void CountryEqualityMixesStringOperatorsAndChecksEveryField()
        {
            Country left = new Country { id = null, age = 1, type = "coppa", overAge = 2 };
            Country right = new Country { id = null, age = 1, type = "coppa", overAge = 2 };
            Assert.IsTrue(left == right);
            Assert.IsFalse(left != right);
            Assert.IsTrue(left.Equals(right));

            right.overAge = 3;
            Assert.IsFalse(left == right, "overAge is compared");
            right.overAge = 2;
            right.type = "COPPA";
            Assert.IsFalse(left == right, "type comparison is ordinal and case-sensitive");

            Country nullType = new Country { type = null };
            Assert.Throws<NullReferenceException>(() => nullType.Equals(new Country { type = null }));
            Assert.Throws<NullReferenceException>(() => { _ = nullType == new Country(); });

            Country nothing = null;
            Assert.IsTrue(nothing == null);
            Assert.IsFalse(nothing == left);
            Assert.IsFalse(left == nothing);
            Assert.IsTrue(left != nothing);
            Assert.IsFalse(left.Equals("coppa"), "a different runtime type is unequal without casting");
        }

        // Country.GetHashCode 0x060009d4 (RVA 0x1AF8210): 397 multiplier, null
        // strings contribute 0, overAge is xored last.
        [Test]
        public void CountryHashUsesOriginalMultiplierAndNullStringZero()
        {
            Assert.AreEqual(157612, new Country { id = null, age = 1, type = null, overAge = 5 }.GetHashCode());
            Assert.AreEqual(1, new Country { id = null, age = 0, type = null, overAge = 1 }.GetHashCode());
            Assert.AreEqual(157609, new Country { id = null, age = 1, type = null, overAge = 0 }.GetHashCode());
        }

        // Country 0x060009d5, RequestAccessData 0x06000980, InitialisationData 0x0600097f.
        [Test]
        public void DataConstructorsKeepOriginalDefaults()
        {
            Country country = new Country();
            Assert.AreEqual(string.Empty, country.id);
            Assert.AreEqual(0, country.age);
            Assert.AreEqual(string.Empty, country.type);
            Assert.AreEqual(-1, country.overAge);
            Assert.IsNull(new CountryRegulationIndexAPI().countries);

            var data = new LegalModule.RequestAccessData();
            Assert.AreEqual(0, data.m_playerAge, "the player age is not preset to DefaultAge");
            Assert.AreEqual(0, data.m_lastConsentedAge);
            Assert.AreEqual(0, data.m_lastConsentedLegalVersion);
            Assert.AreEqual("", data.m_lastConsentedCountryCode);
            Assert.AreEqual("", data.m_lastConsentedAppVersion);
            Assert.AreEqual(LegalModule.AdType.None, data.m_adType);
            Assert.IsFalse(data.m_initialBoot);

            var initialisation = new LegalModule.InitialisationData();
            Assert.IsTrue(initialisation.m_appAimedAtChildren);
            Assert.IsFalse(initialisation.m_appUsesBehaviouralAds);
            Assert.IsNull(initialisation.m_isoCountryCode);
        }

        // Constructor 0x06000974 (RVA 0x1AF1B50): five inputs fail in this order,
        // with the cache name reported as "m_cacheFilePath", before
        // GetSystemRef<CoroutineUtils> can create a registry row.
        [Test]
        public void ConstructorValidatesInputsInOriginalOrderBeforeTheRegistry()
        {
            var data = new LegalModule.InitialisationData();
            ExpectArgument(data, "m_countryRegulationIndexFileURL is null or empty.");
            data.m_countryRegulationIndexFileURL = "https://index";
            ExpectArgument(data, "m_packageCountryRegulationIndexJSON is null or empty.");
            data.m_packageCountryRegulationIndexJSON = "{}";
            ExpectArgument(data, "m_packagedVersionJSON is null or empty.");
            data.m_packagedVersionJSON = "{}";
            ExpectArgument(data, "m_versionFileURL is null or empty.");
            data.m_versionFileURL = "https://version";
            data.m_cacheFileName = "";
            ExpectArgument(data, "m_cacheFilePath is null or empty.");
        }

        // GetCountryConsentType 0x06000960 checks the plugin before the country;
        // GetCountryOverage 0x0600095f has no plugin check and its own wording.
        [Test]
        public void ConstructedModuleGuardsPluginAndCountryAccess()
        {
            using (var fixture = new OwnedModule())
            {
                LegalModule module = fixture.Module;
                Assert.AreEqual(LegalModule.UpdateState.CheckingForUpdates, module.State);
                ExpectUnity(() => module.GetCountryConsentType(), "No plugin as been registered, call RegisterPlugin before RequestAccess");
                ExpectUnity(() => module.GetCountryOverage(), "Country has not been set, call CreateAndInitialise with a valid country.");
                ExpectUnity(() => module.CanShowSetAgeButton(), "No plugin as been registered, call RegisterPlugin before RequestAccess");

                module.RegisterPlugin(new RecordingPlugin());
                ExpectUnity(() => module.GetCountryConsentType(), "Country has no been set, call CreateAndInitialise with a valid country.");

                fixture.Data.m_appAimedAtChildren = false;
                Assert.IsFalse(module.CanShowSetAgeButton(), "the children flag short-circuits the country lookup");

                fixture.SetCountry(new Country { type = "consent", age = 13, overAge = -1 });
                Assert.AreEqual(13, module.GetCountryOverage());
                fixture.SetCountry(new Country { type = "consent", age = 13, overAge = 16 });
                Assert.AreEqual(16, module.GetCountryOverage());
            }
        }

        // GetAgeType 0x06000973 (RVA 0x1AF3A90): without an over-age an unset
        // (-1) age counts as over age, but 0 is under age.
        [Test]
        public void AgeTypeFollowsOriginalThresholds()
        {
            using (var fixture = new OwnedModule())
            {
                var data = new LegalModule.RequestAccessData();
                fixture.Module.SetRequestAccessData(data);
                fixture.SetCountry(new Country { type = "consent", age = 13, overAge = 16 });
                AssertAge(fixture, data, 12, LegalModule.AgeType.Under);
                AssertAge(fixture, data, 13, LegalModule.AgeType.Mid);
                AssertAge(fixture, data, 15, LegalModule.AgeType.Mid);
                AssertAge(fixture, data, 16, LegalModule.AgeType.Over);

                fixture.SetCountry(new Country { type = "consent", age = 13, overAge = -1 });
                AssertAge(fixture, data, -1, LegalModule.AgeType.Over);
                AssertAge(fixture, data, 0, LegalModule.AgeType.Under);
                AssertAge(fixture, data, 12, LegalModule.AgeType.Under);
                AssertAge(fixture, data, 13, LegalModule.AgeType.Over);
            }
        }

        // OptOut 0x06000955 (RVA 0x1AF2950): the opt-out button check matches
        // "consent" ignoring case, resets consent then ads, saves, then revokes
        // with dueToOptOut true.
        [Test]
        public void OptOutResetsConsentAndAdsThenRevokes()
        {
            using (var fixture = new OwnedModule())
            {
                LegalModule module = fixture.Module;
                ExpectUnity(() => module.OptOut(), "No plugin as been registered, call RegisterPlugin before OptOut");
                var plugin = new RecordingPlugin();
                module.RegisterPlugin(plugin);
                LegalModule.RequestAccessData data = ConsentedData();
                module.SetRequestAccessData(data);

                fixture.SetCountry(new Country { type = "coppa", age = 13 });
                module.OptOut();
                CollectionAssert.IsEmpty(plugin.Calls, "a non-consent country is left untouched");
                Assert.AreEqual(20, data.m_lastConsentedAge);

                fixture.SetCountry(new Country { type = "CONSENT", age = 13 });
                module.OptOut();
                CollectionAssert.AreEqual(new[] { "OnDataChange", "OnAccessRevoked:True" }, plugin.Calls);
                Assert.AreSame(data, plugin.LastData);
                Assert.AreEqual(20, data.m_playerAge);
                Assert.AreEqual(0, data.m_lastConsentedAge);
                Assert.AreEqual(0, data.m_lastConsentedTargetAge);
                Assert.AreEqual(0, data.m_lastConsentedLegalVersion);
                Assert.AreEqual(0, data.m_lastConsentedCountryVersion);
                Assert.AreEqual(0, data.m_lastConsentedAdAge);
                Assert.AreEqual(0, data.m_lastConsentedTargetAdAge);
                Assert.AreEqual("", data.m_lastConsentedCountryCode);
                Assert.AreEqual("", data.m_lastConsentedAppVersion);
                Assert.AreEqual(LegalModule.AdType.None, data.m_adType);
                Assert.AreEqual(LegalModule.AdType.Generic, fixture.Read<LegalModule.AdType>("m_previousAdType"));
            }
        }

        // OptOutAds 0x06000956 (RVA 0x1AF2C00): only a non-COPPA country whose
        // consented ad age equals the current (adult) age resets the ad fields;
        // consent fields survive.
        [Test]
        public void OptOutAdsRequiresAConsentedAdAgeMatchingThePlayer()
        {
            using (var fixture = new OwnedModule())
            {
                LegalModule module = fixture.Module;
                var plugin = new RecordingPlugin();
                module.RegisterPlugin(plugin);
                LegalModule.RequestAccessData data = ConsentedData();
                module.SetRequestAccessData(data);

                fixture.SetCountry(new Country { type = "Coppa", age = 13 });
                Assert.IsFalse(module.CanShowOptOutAdsButton(), "COPPA is matched ignoring case");
                fixture.SetCountry(new Country { type = "ccpa", age = 21 });
                Assert.IsFalse(module.CanShowOptOutAdsButton(), "the player must reach the country age");
                fixture.SetCountry(new Country { type = "ccpa", age = 13 });
                fixture.Write("m_legalFlowState", LegalModule.LegalFlowState.Complete);
                data.m_lastConsentedAdAge = 19;
                module.OptOutAds();
                CollectionAssert.IsEmpty(plugin.Calls);
                Assert.AreEqual(LegalModule.LegalFlowState.Complete, fixture.Read<LegalModule.LegalFlowState>("m_legalFlowState"), "a rejected opt-out keeps the flow state");
                Assert.AreEqual(LegalModule.AdType.None, fixture.Read<LegalModule.AdType>("m_previousAdType"), "a rejected opt-out keeps the previous ad type");

                data.m_lastConsentedAdAge = 20;
                module.OptOutAds();
                CollectionAssert.AreEqual(new[] { "OnDataChange", "OnAccessRevoked:True" }, plugin.Calls);
                // 0x1AF2C99 clears m_legalFlowState before ResetAdsAccessData records m_previousAdType.
                Assert.AreEqual(LegalModule.LegalFlowState.None, fixture.Read<LegalModule.LegalFlowState>("m_legalFlowState"));
                Assert.AreEqual(LegalModule.AdType.Generic, fixture.Read<LegalModule.AdType>("m_previousAdType"));
                Assert.AreEqual(0, data.m_lastConsentedAdAge);
                Assert.AreEqual(0, data.m_lastConsentedTargetAdAge);
                Assert.AreEqual(LegalModule.AdType.None, data.m_adType);
                Assert.AreEqual(20, data.m_lastConsentedAge, "consent fields are kept");
                Assert.AreEqual("GB", data.m_lastConsentedCountryCode);
            }
        }

        // ChangeAge 0x0600095a (RVA 0x1AF2EF0): the regulated-country test uses
        // exact string == through IsCountryGDPR/CCPA/COPPA, so "CONSENT" only
        // saves the age while "consent" also resets and revokes with false.
        [Test]
        public void ChangeAgeRevokesOnlyForExactRegulatedTypes()
        {
            using (var fixture = new OwnedModule())
            {
                LegalModule module = fixture.Module;
                var plugin = new RecordingPlugin();
                module.RegisterPlugin(plugin);
                LegalModule.RequestAccessData data = ConsentedData();
                module.SetRequestAccessData(data);

                fixture.SetCountry(new Country { type = "CONSENT", age = 13 });
                module.ChangeAge(20);
                CollectionAssert.IsEmpty(plugin.Calls, "an unchanged age does nothing");
                module.ChangeAge(18);
                CollectionAssert.AreEqual(new[] { "OnDataChange" }, plugin.Calls);
                Assert.AreEqual(18, data.m_playerAge);
                Assert.AreEqual(20, data.m_lastConsentedAge);

                plugin.Calls.Clear();
                fixture.SetCountry(new Country { type = "consent", age = 13 });
                module.ChangeAge(17);
                CollectionAssert.AreEqual(new[] { "OnDataChange", "OnAccessRevoked:False" }, plugin.Calls);
                Assert.AreEqual(17, data.m_playerAge);
                Assert.AreEqual(0, data.m_lastConsentedAge);
                Assert.AreEqual(LegalModule.AdType.None, data.m_adType);
            }
        }

        // ConsentIsValid 0x06000966 compares the cached legal version (L);
        // AdConsentIsValid 0x06000968 (RVA 0x1AF37C0) takes the ad type from the
        // module's request data, not from its argument.
        [Test]
        public void ConsentChecksUseCachedVersionAndModuleAdType()
        {
            using (var fixture = new OwnedModule())
            {
                var cache = new LegalModule.LegalModuleCacheAPI { m_versionData = new VersionAPI { L = 3, C = 9 } };
                fixture.Write("m_cacheData", cache);
                var argument = new LegalModule.RequestAccessData { m_playerAge = 20, m_lastConsentedAge = 20, m_lastConsentedLegalVersion = 3 };
                Assert.IsTrue(fixture.Invoke<bool>("ConsentIsValid", argument));
                argument.m_lastConsentedLegalVersion = 2;
                Assert.IsFalse(fixture.Invoke<bool>("ConsentIsValid", argument), "an older legal version needs consent");
                argument.m_lastConsentedLegalVersion = 4;
                argument.m_lastConsentedAge = 0;
                Assert.IsFalse(fixture.Invoke<bool>("ConsentIsValid", argument), "no recorded consent");

                var moduleData = new LegalModule.RequestAccessData { m_adType = LegalModule.AdType.Generic };
                fixture.Module.SetRequestAccessData(moduleData);
                argument.m_lastConsentedAdAge = 20;
                argument.m_adType = LegalModule.AdType.None;
                Assert.IsTrue(fixture.Invoke<bool>("AdConsentIsValid", argument));
                moduleData.m_adType = LegalModule.AdType.None;
                argument.m_adType = LegalModule.AdType.Behavioural;
                Assert.IsFalse(fixture.Invoke<bool>("AdConsentIsValid", argument));
            }
        }

        // dump.cs 0x0200014d: generated names fix member ordinals, so the nine
        // nested types (0x0200014e..0x02000156) precede LegalFlow. Display-class
        // field order is configuration-independent; iterator hoists are not and
        // are left out.
        [Test]
        public void DeclarationsKeepOriginalOrdinalsAndAttributes()
        {
            var iterators = new Dictionary<string, string>
            {
                { "LegalFlow", "<LegalFlow>d__52" },
                { "RetrieveLegalDocumentURLCoroutine", "<RetrieveLegalDocumentURLCoroutine>d__68" },
                { "CheckForPlayerAge", "<CheckForPlayerAge>d__71" },
                { "CheckForConsent", "<CheckForConsent>d__74" },
                { "CheckForAdImprovementConsent", "<CheckForAdImprovementConsent>d__76" },
                { "CheckForAdImprovementConfirmation", "<CheckForAdImprovementConfirmation>d__77" },
                { "CheckForAdType", "<CheckForAdType>d__78" },
                { "CheckForTrackingRequestConformation", "<CheckForTrackingRequestConformation>d__79" },
                { "StartVersionFileDownload", "<StartVersionFileDownload>d__92" },
                { "StartCountryRegulationIndexFileDownload", "<StartCountryRegulationIndexFileDownload>d__93" },
            };
            foreach (KeyValuePair<string, string> iterator in iterators)
            {
                var attribute = typeof(LegalModule).GetMethod(iterator.Key, Instance).GetCustomAttribute<IteratorStateMachineAttribute>();
                Assert.IsNotNull(attribute, iterator.Key);
                Assert.AreEqual(iterator.Value, attribute.StateMachineType.Name, iterator.Key);
            }

            var closures = new Dictionary<string, string[]>
            {
                { "<>c__DisplayClass71_0", new[] { "<>4__this", "awaitingAge" } },
                { "<>c__DisplayClass74_0", new[] { "<>4__this", "awaitingConsent" } },
                { "<>c__DisplayClass76_0", new[] { "<>4__this", "legalFlowState", "awaitingConsent" } },
                { "<>c__DisplayClass77_0", new[] { "<>4__this", "awaitingConsent", "legalFlowState" } },
                { "<>c__DisplayClass78_0", new[] { "<>4__this", "plugin", "awaitingAdTypeSelection", "legalFlowState" } },
            };
            foreach (KeyValuePair<string, string[]> closure in closures)
            {
                Type type = typeof(LegalModule).GetNestedType(closure.Key, BindingFlags.NonPublic);
                Assert.IsNotNull(type, closure.Key);
                CollectionAssert.AreEqual(closure.Value,
                    type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly).Select(field => field.Name), closure.Key);
            }

            Type[] nested =
            {
                typeof(LegalModule.AdType), typeof(LegalModule.LegalFlowState), typeof(LegalModule.AgeType),
                typeof(LegalModule.TrackingAuthorisationStatus), typeof(LegalModule.InitialisationData),
                typeof(LegalModule.RequestAccessData), typeof(LegalModule.LegalModuleCacheAPI),
                typeof(LegalModule.UpdateState), typeof(LegalModule.LegalDocumentType),
            };
            for (int i = 1; i < nested.Length; i++)
                Assert.Less(nested[i - 1].MetadataToken, nested[i].MetadataToken, nested[i].Name);

            Option[] options = typeof(LegalModule).GetCustomAttributesData()
                .Where(data => data.AttributeType == typeof(Il2CppSetOptionAttribute))
                .Select(data => (Option)data.ConstructorArguments[0].Value).ToArray();
            CollectionAssert.AreEqual(new[] { Option.ArrayBoundsChecks, Option.NullChecks }, options);

            MethodInfo log = typeof(LegalModule).GetMethod("Log", Instance);
            Assert.IsTrue(log.IsPrivate);
            Assert.AreEqual("UNITY_EDITOR", log.GetCustomAttribute<System.Diagnostics.ConditionalAttribute>().ConditionString);

            ConstructorInfo[] constructors = typeof(LegalModule).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.AreEqual(1, constructors.Length);
            Assert.IsTrue(constructors[0].IsFamily, "the constructor is protected");
        }

        // RequestAccess 0x0600094f (RVA 0x1AF2420): the upgrade check runs first,
        // a country mismatch (OrdinalIgnoreCase, 0x1AF252F) forces reconsent only
        // for behavioural-ads apps, the flag accumulates by OR, then the tracking
        // status is sampled and the flow state cleared before the host fetch.
        [Test]
        public void RequestAccessFoldsReconsentAndSamplesTracking()
        {
            using (var bridge = new TrackingBridge { Status = 2 })
            using (var fixture = new OwnedModule())
            {
                LegalModule module = fixture.Module;
                var data = new LegalModule.RequestAccessData { m_lastConsentedCountryCode = "FR", m_lastConsentedAppVersion = "1.0" };
                ExpectUnity(() => module.RequestAccess(data), "No plugin as been registered, call RegisterPlugin before RequestAccess");
                CollectionAssert.IsEmpty(bridge.Calls, "the plugin check precedes everything");

                module.RegisterPlugin(new RecordingPlugin());
                fixture.UseAbsentCoroutineHost();
                fixture.Write("m_legalFlowState", LegalModule.LegalFlowState.Complete);
                Assert.Throws<NullReferenceException>(() => module.RequestAccess(data, true, false));
                Assert.IsFalse(fixture.Read<bool>("m_requireReconsent"), "a country change alone needs behavioural ads");
                Assert.AreEqual(LegalModule.TrackingAuthorisationStatus.Denied, fixture.Read<LegalModule.TrackingAuthorisationStatus>("m_trackingAuthorisationStatus"));
                CollectionAssert.AreEqual(new[] { "Status" }, bridge.Calls);
                Assert.AreEqual(LegalModule.LegalFlowState.None, fixture.Read<LegalModule.LegalFlowState>("m_legalFlowState"));
                Assert.IsTrue(fixture.Read<bool>("m_rejectionAllowed"));
                Assert.IsFalse(fixture.Read<bool>("m_sendAgeDataToServer"));
                Assert.AreSame(data, fixture.Read<LegalModule.RequestAccessData>("m_requestAccessData"));

                fixture.Data.m_appUsesBehaviouralAds = true;
                data.m_lastConsentedCountryCode = "gb";
                Assert.Throws<NullReferenceException>(() => module.RequestAccess(data));
                Assert.IsFalse(fixture.Read<bool>("m_requireReconsent"), "the country code compare ignores case");
                data.m_lastConsentedCountryCode = "FR";
                Assert.Throws<NullReferenceException>(() => module.RequestAccess(data));
                Assert.IsTrue(fixture.Read<bool>("m_requireReconsent"));
                data.m_lastConsentedCountryCode = "GB";
                Assert.Throws<NullReferenceException>(() => module.RequestAccess(data));
                Assert.IsTrue(fixture.Read<bool>("m_requireReconsent"), "an earlier reconsent is kept");

                fixture.Write("m_requireReconsent", false);
                Assert.Throws<NullReferenceException>(() => module.RequestAccess(data, requireReconsent: true));
                Assert.IsTrue(fixture.Read<bool>("m_requireReconsent"), "the argument forces reconsent");

                // SaveDataUpgradeRequired 0x06000950: an old save gains the current
                // country and app version before the mismatch test reads them.
                fixture.Write("m_requireReconsent", false);
                var upgraded = new LegalModule.RequestAccessData { m_lastConsentedLegalVersion = 1, m_lastConsentedCountryCode = "FR" };
                Assert.Throws<NullReferenceException>(() => module.RequestAccess(upgraded));
                Assert.AreEqual("GB", upgraded.m_lastConsentedCountryCode);
                Assert.AreEqual("1.10.1", upgraded.m_lastConsentedAppVersion);
                Assert.IsFalse(fixture.Read<bool>("m_requireReconsent"));
            }
        }

        // RequestAccess 0x0600094e (RVA 0x1AF2260) and FindCountry 0x06000962
        // (RVA 0x1AF23B0): a non-empty code replaces the initialisation code and
        // reselects ignoring case; an unknown or null code gives DefaultCountry.
        [Test]
        public void CountrySelectionIgnoresCaseAndFallsBackToDefault()
        {
            using (new TrackingBridge())
            using (var fixture = new OwnedModule())
            {
                LegalModule module = fixture.Module;
                var gb = new Country { id = "GB", age = 13, type = "consent" };
                var fr = new Country { id = "FR", age = 15, type = "consent" };
                var countries = new[] { gb, fr };
                fixture.Write("m_cacheData", new LegalModule.LegalModuleCacheAPI
                {
                    m_versionData = new VersionAPI(),
                    m_countryIndex = new CountryRegulationIndexAPI { countries = countries },
                });
                module.RegisterPlugin(new RecordingPlugin());
                fixture.UseAbsentCoroutineHost();
                fixture.SetCountry(gb);
                var data = new LegalModule.RequestAccessData { m_lastConsentedCountryCode = "GB", m_lastConsentedAppVersion = "1.10.1" };

                Assert.Throws<NullReferenceException>(() => module.RequestAccess(data, "fr"));
                Assert.AreEqual("fr", fixture.Data.m_isoCountryCode);
                Assert.AreSame(fr, fixture.Read<Country>("m_currentCountry"));

                Assert.Throws<NullReferenceException>(() => module.RequestAccess(data, ""));
                Assert.AreEqual("fr", fixture.Data.m_isoCountryCode, "an empty code keeps the initialisation code");
                Assert.AreSame(fr, fixture.Read<Country>("m_currentCountry"), "an empty code keeps the country");

                Country fallback = fixture.Read<Country>("DefaultCountry");
                Assert.AreEqual("default", fallback.id);
                Assert.AreEqual(13, fallback.age);
                Assert.AreEqual("coppa", fallback.type);
                Assert.AreEqual(-1, fallback.overAge);
                Assert.Throws<NullReferenceException>(() => module.RequestAccess(data, "XX"));
                Assert.AreSame(fallback, fixture.Read<Country>("m_currentCountry"));
                Assert.AreSame(fallback, fixture.Invoke<Country>("FindCountry", null, countries));
                Assert.AreSame(gb, fixture.Invoke<Country>("FindCountry", "gB", countries));
            }
        }

        // SetupCountryData 0x06000963 (RVA 0x1AF35C0), RequiresImproveExperiencePrompt
        // 0x06000970 (0x1AF3A40), ImproveExperienceExcluded 0x06000971 (0x1AF3AD0)
        // and RequiresImproveExperienceConfirmation 0x06000972 (0x1AF3B20).
        [Test]
        public void ImproveExperienceGatesFollowCountryAndAgeClass()
        {
            using (var bridge = new TrackingBridge())
            using (var fixture = new OwnedModule())
            {
                var data = new LegalModule.RequestAccessData();
                fixture.Module.SetRequestAccessData(data);

                AssertCountryFlags(fixture, "Consent", false, true, false);
                AssertCountryFlags(fixture, "CCPA", true, true, true);
                fixture.Write("m_improveExperienceMidAge", true);
                fixture.Write("m_improveExperienceOverAge", true);
                fixture.Write("m_improveExperienceExcludedFromBootFlowOverage", true);
                AssertCountryFlags(fixture, "coppa", false, false, false);
                AssertCountryFlags(fixture, "none", false, false, false);

                // Consent: only the over flag is set; under age reads it too.
                fixture.SetCountry(new Country { type = "consent", age = 13, overAge = 16 });
                fixture.Invoke("SetupCountryData");
                data.m_playerAge = 14;
                Assert.IsFalse(fixture.Invoke<bool>("RequiresImproveExperiencePrompt"), "mid age reads the mid flag");
                data.m_playerAge = 10;
                Assert.IsTrue(fixture.Invoke<bool>("RequiresImproveExperiencePrompt"), "under age reads the over flag");
                Assert.IsTrue(fixture.Invoke<bool>("ImproveExperienceExcluded"), "under age is always excluded");

                // CCPA: over-age players are excluded only on the initial boot.
                fixture.SetCountry(new Country { type = "ccpa", age = 13, overAge = 16 });
                fixture.Invoke("SetupCountryData");
                data.m_playerAge = 14;
                data.m_initialBoot = true;
                Assert.IsTrue(fixture.Invoke<bool>("RequiresImproveExperiencePrompt"));
                Assert.IsFalse(fixture.Invoke<bool>("ImproveExperienceExcluded"), "mid age is never excluded");
                data.m_playerAge = 18;
                Assert.IsTrue(fixture.Invoke<bool>("ImproveExperienceExcluded"));
                data.m_initialBoot = false;
                Assert.IsFalse(fixture.Invoke<bool>("ImproveExperienceExcluded"));

                Assert.IsFalse(fixture.Invoke<bool>("RequiresImproveExperienceConfirmation"), "over age needs no confirmation");
                CollectionAssert.IsEmpty(bridge.Calls, "the age class is read before the bridge");
                data.m_playerAge = 14;
                Assert.IsTrue(fixture.Invoke<bool>("RequiresImproveExperienceConfirmation"), "no tracking prompt available");
                CollectionAssert.AreEqual(new[] { "Can" }, bridge.Calls);
                bridge.CanRequest = true;
                bridge.Status = 0;
                Assert.IsFalse(fixture.Invoke<bool>("RequiresImproveExperienceConfirmation"), "tracking still undetermined");
                bridge.Status = 3;
                Assert.IsTrue(fixture.Invoke<bool>("RequiresImproveExperienceConfirmation"));

                fixture.SetCountry(new Country { type = "ccpa", age = 13, overAge = -1 });
                Assert.IsFalse(fixture.Invoke<bool>("RequiresImproveExperienceConfirmation"), "without an over-age there is no mid class");
            }
        }

        // CurrentCountryHasReceivedUpdate 0x0600097b (RVA 0x1AF4160) compares by
        // Country value; SaveDataUpgradeRequired 0x06000950 (RVA 0x1AF2700)
        // compares the saved strings against string.Empty.
        [Test]
        public void CountryUpdateAndSaveUpgradeChecks()
        {
            using (var fixture = new OwnedModule())
            {
                var index = new CountryRegulationIndexAPI { countries = new[] { new Country { id = "GB", age = 13, type = "consent" } } };
                Assert.IsTrue(fixture.Invoke<bool>("CurrentCountryHasReceivedUpdate", index), "no current country");
                fixture.SetCountry(new Country { id = "GB", age = 13, type = "consent" });
                Assert.IsFalse(fixture.Invoke<bool>("CurrentCountryHasReceivedUpdate", index), "an equal copy is no update");
                index.countries[0].age = 16;
                Assert.IsTrue(fixture.Invoke<bool>("CurrentCountryHasReceivedUpdate", index));
                fixture.SetCountry(new Country { id = "default", age = 13, type = "coppa" });
                Assert.IsFalse(fixture.Invoke<bool>("CurrentCountryHasReceivedUpdate", new CountryRegulationIndexAPI { countries = new Country[0] }),
                    "a missing country falls back to an equal DefaultCountry");

                var data = new LegalModule.RequestAccessData();
                Assert.IsFalse(fixture.Invoke<bool>("SaveDataUpgradeRequired", data), "never consented");
                data.m_lastConsentedLegalVersion = 1;
                Assert.IsTrue(fixture.Invoke<bool>("SaveDataUpgradeRequired", data));
                data.m_lastConsentedCountryCode = "GB";
                Assert.IsTrue(fixture.Invoke<bool>("SaveDataUpgradeRequired", data), "the app version is still empty");
                data.m_lastConsentedAppVersion = "1.0";
                Assert.IsFalse(fixture.Invoke<bool>("SaveDataUpgradeRequired", data));
                data.m_lastConsentedCountryCode = null;
                Assert.IsFalse(fixture.Invoke<bool>("SaveDataUpgradeRequired", data), "null is not string.Empty");
            }
        }

        // <LegalFlow>d__52.MoveNext (RVA 0x1AF6050): waits while checking for
        // updates, sets up country data, applies a pending reconsent, then
        // dispatches: None -> AgeGate, GrantAccess -> OnAccessGranted then
        // Complete, Complete -> stop, Error -> Exception, > Error -> range fault.
        [Test]
        public void LegalFlowDispatchesTheOriginalStateTable()
        {
            using (var fixture = new OwnedModule())
            {
                var plugin = new RecordingPlugin();
                fixture.Module.RegisterPlugin(plugin);
                LegalModule.RequestAccessData data = ConsentedData();
                fixture.Module.SetRequestAccessData(data);
                fixture.SetCountry(new Country { type = "consent", age = 13 });

                fixture.Write("m_legalFlowState", LegalModule.LegalFlowState.Complete);
                IEnumerator flow = fixture.Invoke<IEnumerator>("LegalFlow");
                Assert.IsTrue(flow.MoveNext(), "waits while checking for updates");
                Assert.IsNull(flow.Current);
                Assert.IsFalse(fixture.Read<bool>("m_improveExperienceOverAge"), "country data waits for the update check");
                fixture.SetState(LegalModule.UpdateState.UpdateFailed);
                Assert.IsFalse(flow.MoveNext(), "a failed update still runs the flow");
                Assert.IsTrue(fixture.Read<bool>("m_improveExperienceOverAge"));
                CollectionAssert.IsEmpty(plugin.Calls);

                fixture.Write("m_requireReconsent", true);
                Assert.IsFalse(fixture.Invoke<IEnumerator>("LegalFlow").MoveNext());
                CollectionAssert.AreEqual(new[] { "OnDataChange" }, plugin.Calls);
                Assert.IsFalse(fixture.Read<bool>("m_requireReconsent"));
                Assert.AreEqual(0, data.m_lastConsentedAge);
                Assert.AreEqual(LegalModule.AdType.None, data.m_adType);
                Assert.AreEqual(LegalModule.AdType.Generic, fixture.Read<LegalModule.AdType>("m_previousAdType"));

                plugin.Calls.Clear();
                fixture.Write("m_useRestrictedAds", true);
                fixture.Write("m_legalFlowState", LegalModule.LegalFlowState.GrantAccess);
                Assert.IsFalse(fixture.Invoke<IEnumerator>("LegalFlow").MoveNext());
                CollectionAssert.AreEqual(new[] { "OnAccessGranted:True" }, plugin.Calls);
                Assert.AreEqual(LegalModule.LegalFlowState.Complete, fixture.Read<LegalModule.LegalFlowState>("m_legalFlowState"));

                fixture.Write("m_legalFlowState", LegalModule.LegalFlowState.None);
                flow = fixture.Invoke<IEnumerator>("LegalFlow");
                Assert.IsTrue(flow.MoveNext());
                Assert.IsInstanceOf<IEnumerator>(flow.Current, "the age gate is yielded as a nested coroutine");
                Assert.AreEqual(LegalModule.LegalFlowState.AgeGate, fixture.Read<LegalModule.LegalFlowState>("m_legalFlowState"));

                fixture.Write("m_legalFlowState", LegalModule.LegalFlowState.Error);
                var error = Assert.Throws<Exception>(() => fixture.Invoke<IEnumerator>("LegalFlow").MoveNext());
                Assert.AreEqual("Legal flow errored.", error.Message);
                fixture.Write("m_legalFlowState", (LegalModule.LegalFlowState)10);
                Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Invoke<IEnumerator>("LegalFlow").MoveNext());
            }
        }

        // <CheckForPlayerAge>d__71.MoveNext and <>c__DisplayClass71_0.b__0
        // (RVA 0x1AF42F0): only an age-gated country with a non-positive age asks;
        // the answer is saved only when age data goes to the server.
        [Test]
        public void PlayerAgeIsRequestedOnlyWhenUnset()
        {
            using (var fixture = new OwnedModule())
            {
                var plugin = new RecordingPlugin();
                fixture.Module.RegisterPlugin(plugin);
                var data = new LegalModule.RequestAccessData { m_playerAge = 20 };
                fixture.Module.SetRequestAccessData(data);
                fixture.SetCountry(new Country { type = "consent", age = 13 });

                Assert.IsFalse(fixture.Invoke<IEnumerator>("CheckForPlayerAge").MoveNext());
                CollectionAssert.IsEmpty(plugin.Calls, "a known age is not requested");
                Assert.AreEqual(LegalModule.LegalFlowState.Consent, fixture.Read<LegalModule.LegalFlowState>("m_legalFlowState"));

                data.m_playerAge = 0;
                fixture.Write("m_sendAgeDataToServer", true);
                IEnumerator check = fixture.Invoke<IEnumerator>("CheckForPlayerAge");
                Assert.IsTrue(check.MoveNext());
                Assert.IsTrue(check.MoveNext(), "waits for the answer");
                plugin.AgeCallback(15);
                Assert.AreEqual(15, data.m_playerAge);
                Assert.IsFalse(check.MoveNext());
                CollectionAssert.AreEqual(new[] { "RequestPlayerAge", "OnDataChange" }, plugin.Calls);

                plugin.Calls.Clear();
                data.m_playerAge = -1;
                fixture.Write("m_sendAgeDataToServer", false);
                check = fixture.Invoke<IEnumerator>("CheckForPlayerAge");
                Assert.IsTrue(check.MoveNext());
                plugin.AgeCallback(16);
                Assert.IsFalse(check.MoveNext());
                CollectionAssert.AreEqual(new[] { "RequestPlayerAge" }, plugin.Calls, "the answer is not saved");

                plugin.Calls.Clear();
                data.m_playerAge = 0;
                fixture.Data.m_appAimedAtChildren = false;
                Assert.IsFalse(fixture.Invoke<IEnumerator>("CheckForPlayerAge").MoveNext());
                CollectionAssert.IsEmpty(plugin.Calls, "no age gate for apps not aimed at children");
            }
        }

        private static void AssertCountryFlags(OwnedModule fixture, string type, bool mid, bool over, bool excluded)
        {
            fixture.SetCountry(new Country { type = type, age = 13 });
            fixture.Invoke("SetupCountryData");
            Assert.AreEqual(mid, fixture.Read<bool>("m_improveExperienceMidAge"), type + " mid");
            Assert.AreEqual(over, fixture.Read<bool>("m_improveExperienceOverAge"), type + " over");
            Assert.AreEqual(excluded, fixture.Read<bool>("m_improveExperienceExcludedFromBootFlowOverage"), type + " excluded");
        }

        private static LegalModule.RequestAccessData ConsentedData() => new LegalModule.RequestAccessData
        {
            m_playerAge = 20,
            m_lastConsentedAge = 20,
            m_lastConsentedTargetAge = 13,
            m_lastConsentedLegalVersion = 2,
            m_lastConsentedCountryVersion = 4,
            m_lastConsentedAdAge = 20,
            m_lastConsentedTargetAdAge = 13,
            m_lastConsentedCountryCode = "GB",
            m_lastConsentedAppVersion = "1.10.1",
            m_adType = LegalModule.AdType.Generic,
        };

        private static void AssertAge(OwnedModule fixture, LegalModule.RequestAccessData data, int age, LegalModule.AgeType expected)
        {
            data.m_playerAge = age;
            Assert.AreEqual(expected, fixture.Module.GetAgeType(), "age " + age);
        }

        // Validation must fail before GetSystemRef<CoroutineUtils>. An unexpected
        // construction is cleaned up here so its row cannot leak into later cases:
        // a borrowed row drops the typed reference it gained, an absent row is
        // unregistered and removed again.
        private static void ExpectArgument(LegalModule.InitialisationData data, string message)
        {
            string row = ProcessManager.GetDefaultName<CoroutineUtils>();
            BorrowedSystemRowTypedCache borrowed = BorrowedSystemRowTypedCache.TryBorrow(row);
            bool rowCreated;
            try
            {
                var exception = Assert.Throws<ArgumentException>(() => new ProbeLegalModule(data));
                Assert.AreEqual(message, exception.Message);
            }
            finally
            {
                rowCreated = borrowed == null && BorrowedSystemRowTypedCache.Registry.Contains(row);
                if (borrowed != null)
                {
                    borrowed.Dispose();
                }
                else if (rowCreated)
                {
                    ProcessManager.UnregisterSystem(row);
                    BorrowedSystemRowTypedCache.Registry.Remove(row);
                }
            }
            Assert.IsFalse(rowCreated, "validation precedes the CoroutineUtils registry lookup");
        }

        private static void ExpectUnity(TestDelegate action, string message)
        {
            var exception = Assert.Throws<UnityException>(action);
            Assert.AreEqual(message, exception.Message);
        }

        private sealed class ProbeLegalModule : LegalModule
        {
            public ProbeLegalModule(InitialisationData data) : base(data) { }
        }

        private sealed class RecordingPlugin : ILegalPlugin
        {
            public readonly List<string> Calls = new List<string>();
            public LegalModule.RequestAccessData LastData;

            public Action<int> AgeCallback;

            public void RequestPlayerAge(Action<int> callback)
            {
                Calls.Add("RequestPlayerAge");
                AgeCallback = callback;
            }
            public void RequestConsent(bool underAge, Action callback) => Calls.Add("RequestConsent");
            public void RequestConsent(bool underAge, Action callbackAccept, Action callbackReject) => Calls.Add("RequestConsentRejectable");

            public void OnDataChange(LegalModule.RequestAccessData newDataToSave)
            {
                Calls.Add("OnDataChange");
                LastData = newDataToSave;
            }

            public void OnAccessGranted(bool restrictedMode) => Calls.Add("OnAccessGranted:" + restrictedMode);
            public void OnAccessRevoked(bool dueToOptOut) => Calls.Add("OnAccessRevoked:" + dueToOptOut);
            public string GetLegalDocumentsFileURL() => null;
        }

        // Swaps HLUnityCore.s_trackingAuthorisationNativeBridge for a recording
        // bridge and restores the previous value.
        private sealed class TrackingBridge : IHLTrackingAuthorisationNativeBridge, IDisposable
        {
            private static readonly FieldInfo Field = typeof(HLUnityCore).GetField("s_trackingAuthorisationNativeBridge", BindingFlags.Static | BindingFlags.NonPublic);
            private readonly object m_previous;
            public readonly List<string> Calls = new List<string>();
            public bool CanRequest;
            public int Status = -1;

            public TrackingBridge()
            {
                m_previous = Field.GetValue(null);
                Field.SetValue(null, this);
            }

            public bool CanDeviceRequestTrackingAuthorisation()
            {
                Calls.Add("Can");
                return CanRequest;
            }

            public void RequestTrackingAuthorisation() => Calls.Add("Request");

            public int RequestTrackingAuthorisationStatus()
            {
                Calls.Add("Status");
                return Status;
            }

            public void Dispose()
            {
                object current = Field.GetValue(null);
                Field.SetValue(null, m_previous);
                if (!ReferenceEquals(current, this))
                    throw new InvalidOperationException("Tracking bridge was replaced during the test");
            }
        }

        // Owns (or borrows) the CoroutineUtils registry row that the constructor
        // reads through ProcessManager.GetSystemRef<CoroutineUtils> (0x06000e3f).
        private sealed class OwnedModule : IDisposable
        {
            private readonly VerificationFixtureOwnership.RegistryRow m_row;
            private readonly BorrowedSystemRowTypedCache m_borrowedRow;

            public readonly LegalModule.InitialisationData Data = new LegalModule.InitialisationData
            {
                m_cacheFileName = "lucid-legal-test.json",
                m_versionFileURL = "https://version",
                m_countryRegulationIndexFileURL = "https://index",
                m_packagedVersionJSON = "{}",
                m_packageCountryRegulationIndexJSON = "{}",
                m_appVersion = "1.10.1",
                m_isoCountryCode = "GB",
            };

            public readonly LegalModule Module;

            public OwnedModule()
            {
                string name = ProcessManager.GetDefaultName<CoroutineUtils>();
                m_borrowedRow = BorrowedSystemRowTypedCache.TryBorrow(name);
                if (m_borrowedRow == null)
                {
                    m_row = new VerificationFixtureOwnership.RegistryRow(name);
                    m_row.Register(null);
                }
                try
                {
                    Module = new ProbeLegalModule(Data);
                }
                catch
                {
                    Release();
                    throw;
                }
            }

            public void SetCountry(Country country) => Write("m_currentCountry", country);

            public void Write(string field, object value) => typeof(LegalModule).GetField(field, Instance).SetValue(Module, value);

            public T Read<T>(string field) => (T)typeof(LegalModule).GetField(field, Instance).GetValue(Module);

            public T Invoke<T>(string method, params object[] arguments) =>
                (T)typeof(LegalModule).GetMethod(method, Instance).Invoke(Module, arguments);

            public void Invoke(string method) => typeof(LegalModule).GetMethod(method, Instance).Invoke(Module, null);

            public void SetState(LegalModule.UpdateState state) => typeof(LegalModule).GetProperty("State").SetValue(Module, state);

            // A SystemRef with no system: Get (0x06000e22) faults, so RequestAccess
            // stops at the host fetch without touching the shared registry.
            public void UseAbsentCoroutineHost() =>
                Write("m_coroutineUtils", new SystemRef<CoroutineUtils>("Lucid legal absent host", null));

            // Dispose 0x06000975 touches the host only for started coroutines; none are.
            public void Dispose()
            {
                try
                {
                    Module.Dispose();
                }
                finally
                {
                    Release();
                }
            }

            private void Release()
            {
                try
                {
                    m_row?.Dispose();
                }
                finally
                {
                    m_borrowedRow?.Dispose();
                }
            }
        }
    }
}
