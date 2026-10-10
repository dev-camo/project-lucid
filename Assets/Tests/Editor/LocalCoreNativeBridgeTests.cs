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
        public void OfflineClientCodeReturnsOneAndWritesZero()
        {
            var bridge = new HLUnityCoreNativeBridge();
            uint clientCode = uint.MaxValue;
            Assert.AreEqual(1, bridge.GetClientCode(out clientCode));
            Assert.AreEqual(0u, clientCode);
        }

        [Test]
        public void MissingOwnerFaultsBeforeTheOfflineInitialiseBoundary()
        {
            var bridge = new HLUnityCoreNativeBridge();
            Assert.Throws<NullReferenceException>(() => bridge.Initialise(null));
        }
    }
}
