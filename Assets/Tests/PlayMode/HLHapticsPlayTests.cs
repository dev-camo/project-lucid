using System.Collections;
using NUnit.Framework;
using ProjectLucid.Verification;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class HLHapticsPlayTests
    {
        [Test] public void ManagerAwakeDebugSnapshotAndSafeTriggerOrder() => HLHapticsVerification.ManagerAwakeDebugSnapshotAndSafeTriggerOrder();
        [UnityTest] public IEnumerator NaturalIteratorFinitePositiveWaitRepeatCounts() => HLHapticsVerification.NaturalIteratorFinitePositiveWaitRepeatCounts();
        [UnityTest] public IEnumerator NaturalIteratorLiveOwnedArrayAfterPositiveWait() => HLHapticsVerification.NaturalIteratorLiveOwnedArrayAfterPositiveWait();
    }
}
