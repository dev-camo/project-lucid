using NUnit.Framework;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests.EditMode
{
    public sealed class InputMetadataPreservationTests
    {
        [Test]
        public void OriginalInputSlotsAndStrictRefusalsMatchTheLoadedModule()
        {
            InputMonitorPipelineVerification.Run();
            LogAssert.NoUnexpectedReceived();
        }
    }
}
