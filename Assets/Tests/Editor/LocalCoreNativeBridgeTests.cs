using System;
using System.Globalization;
using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // Controlled managed boundary tests only; no original native or valid host initialization.
    [TestFixture]
    public sealed class LocalCoreNativeBridgeTests
    {
        [Test]
        public void LocalePolicyTracksTwoOwnedCultureScopes()
        {
            var bridge = new HLUnityCoreNativeBridge();
            var saved = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("en-US");
                Assert.AreEqual("en_US", bridge.GetDeviceRawLocale());
                Assert.AreEqual("en", bridge.GetDeviceLanguageCode());
                CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
                Assert.AreEqual("tr_TR", bridge.GetDeviceRawLocale());
                Assert.AreEqual("tr", bridge.GetDeviceLanguageCode());
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
            }
            Assert.AreSame(saved, CultureInfo.CurrentCulture);
        }

        [Test]
        public void CountryPolicyResolvesNeutralCulturesThroughTheirSpecificCulture()
        {
            AssertCountries(("en", "US"), ("fr", "FR"), ("zh-Hans", "CN"));
        }

        [Test]
        public void CountryPolicyReturnsEmptyForUnmappedNeutralCultures()
        {
            // Mono has no specific culture for these neutral cultures.
            AssertCountries(("st", ""), ("pa", ""), ("en", "US"));
        }

        [Test]
        public void CountryPolicyTracksEachSpecificCultureAfterInvariant()
        {
            // Mono caches RegionInfo.CurrentRegion process-wide; the policy must never consult it.
            // Priming that cache here would leak into the Editor, so the primed case is probed outside it.
            AssertCountries(("", "IV"), ("en-GB", "GB"), ("en-US", "US"), ("en-AU", "AU"), ("", "IV"));
        }

        [Test]
        public void OfflineClientCodeReturnsOneAndWritesZero()
        {
            var bridge = new HLUnityCoreNativeBridge();
            uint clientCode = uint.MaxValue;
            Assert.AreEqual(1, bridge.GetClientCode(out clientCode));
            Assert.AreEqual(0u, clientCode);
        }

        // H-062: offline, the development flag never reaches the unrecovered SetDevelopmentBuild import, which
        // would throw EntryPointNotFoundException if it were called.
        [Test]
        public void OfflineMacOSOutputPluginNeverCallsTheDevelopmentBuildImport()
        {
            var plugin = new HLOutputPluginMacOS();
            Assert.DoesNotThrow(() => plugin.InternalSetDevelopmentBuild(true));
            Assert.DoesNotThrow(() => plugin.InternalSetDevelopmentBuild(false));
        }

        // H-063: offline, the native log-error test reports that it is unavailable instead of reaching its import.
        [Test]
        public void OfflineMacOSOutputPluginReportsTheNativeLogErrorTestAsUnsupported()
        {
            Assert.Throws<NotSupportedException>(() => new HLOutputPluginMacOS().InternalTestNativeLogError());
        }

        [Test]
        public void MissingOwnerFaultsBeforeTheOfflineInitialiseBoundary()
        {
            var bridge = new HLUnityCoreNativeBridge();
            Assert.Throws<NullReferenceException>(() => bridge.Initialise(null));
        }

        private static void AssertCountries(params (string culture, string country)[] cases)
        {
            var bridge = new HLUnityCoreNativeBridge();
            var saved = CultureInfo.CurrentCulture;
            try
            {
                foreach (var (culture, country) in cases)
                {
                    CultureInfo.CurrentCulture = new CultureInfo(culture);
                    Assert.AreEqual(country, bridge.GetDeviceISO2CountryCode(), culture);
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
            }
            Assert.AreSame(saved, CultureInfo.CurrentCulture);
        }
    }
}
