using System.Collections;
using NUnit.Framework;
using ProjectLucid.Verification;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class OriginalCoroutineSchedulingPlayTests
    {
        [UnityTest] public IEnumerator OriginalCoroutineActualScheduledFramesStopAndDuplicateLifetime()
        {
            int count = -1;
            yield return OriginalCoroutineEngineVerification.OriginalScheduledFramesStopAndDuplicateLifetime(value => count = value);
            Assert.AreEqual(17, count);
        }
    }
}
