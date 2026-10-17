using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // PlayMode controls for the per-test isolation hook. The verdict also
    // requires each test's own clean line in the playmode isolation report.
    public sealed class SharedStateIsolationPlayModeHookTests
    {
        // Fails if the Test Framework never ran the recorder's TestStarted for this test.
        [Test]
        public void HookCapturedThisTestBeforeItsBody()
        {
            Assert.AreEqual(TestContext.CurrentContext.Test.FullName, SharedStateIsolationRecorder.StartedTest);
        }

        // The start snapshot stays open across frames until the coroutine test finishes.
        [UnityTest]
        public IEnumerator HookStaysOpenAcrossFrames()
        {
            string name = TestContext.CurrentContext.Test.FullName;
            Assert.AreEqual(name, SharedStateIsolationRecorder.StartedTest);
            yield return null;
            yield return null;
            Assert.AreEqual(name, SharedStateIsolationRecorder.StartedTest);
        }
    }
}
