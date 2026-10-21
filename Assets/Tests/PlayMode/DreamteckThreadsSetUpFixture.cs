using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // After every test of the namespace has run, end the SplineThreading pool threads a test may have started through the type
    // initializer (see DreamteckThreadCleanup), so none is alive when the run's domain unloads.
    [SetUpFixture]
    public sealed class DreamteckThreadsSetUpFixture
    {
        [OneTimeTearDown]
        public void StopTheSplineThreadingPool()
        {
            DreamteckThreadCleanup.StopAndJoin();
        }
    }
}
