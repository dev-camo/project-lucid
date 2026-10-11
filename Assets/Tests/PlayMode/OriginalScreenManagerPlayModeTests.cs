using NUnit.Framework;
using ProjectLucid.Tests.ScreenShared;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class OriginalScreenManagerPlayModeTests
    {
        [Test] public void GenuinePlayLifecycleSavesOwnedPropertiesAndTearsDown() => ScreenManagerVerification.GenuinePlayLifecycleSavesOwnedPropertiesAndTearsDown();
    }
}
