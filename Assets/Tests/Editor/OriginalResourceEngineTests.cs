using NUnit.Framework;
using ProjectLucid.RecoveredProof;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public class OriginalResourceEngineTests
    {
        [Test] public void CurrentSceneNameReadsOwnedActiveScene() => Assert.AreEqual(6, OriginalResourceEngineVerification.CurrentSceneNameReadsOwnedActiveScene());
        [Test] public void UnityShuffleConsumesOneDrawBeforeReadsAndRestoresState() => Assert.AreEqual(9, OriginalResourceEngineVerification.UnityShuffleConsumesOneDrawBeforeReadsAndRestoresState());
    }
}
