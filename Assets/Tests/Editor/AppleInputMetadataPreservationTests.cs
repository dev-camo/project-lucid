using NUnit.Framework;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests.EditMode
{
    public sealed class AppleInputMetadataPreservationTests
    {
        [Test]
        public void SourceApplePluginControllerProviderGettersKeepTheirOriginalSlotFlags()
        {
            ApplePluginProviderPipelineVerification.Run();
            LogAssert.NoUnexpectedReceived();
        }
    }
}
