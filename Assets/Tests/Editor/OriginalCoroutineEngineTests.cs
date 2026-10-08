using NUnit.Framework;
using ProjectLucid.Verification;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class OriginalCoroutineEngineTests
    {
        [Test] public void OriginalCoroutineActualScaledUnscaledAndRealtimeGetters() => Assert.AreEqual(15, OriginalCoroutineEngineVerification.OriginalScaledUnscaledAndRealtimeGetters());
        [Test] public void OriginalCoroutineActualRegistrationPublicationFaultPrefix() => Assert.AreEqual(11, OriginalCoroutineEngineVerification.OriginalRegistrationPublicationAndManagedFaultPrefix());
        [Test] public void OriginalCoroutineActualDestroyedHostRetainsUnityNullReference() => Assert.AreEqual(15, OriginalCoroutineEngineVerification.OriginalRealHostDestroyRetainsUnityNullReference());
    }
}
