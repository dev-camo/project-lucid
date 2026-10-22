using NUnit.Framework;

namespace ProjectLucid
{
    // The global UnityEngine.Random state is borrowed by fixtures whose production code or arrangement draws from it (shuffles,
    // spreads, random delays). SharedStateSnapshot leaves Random.state out, so each such fixture puts back the incoming state
    // after every case, also when the case fails. Cases that need a deterministic draw still seed it themselves.
    public abstract class RandomStateFixture
    {
        private UnityEngine.Random.State incomingRandomState;

        [SetUp]
        public void SaveIncomingRandomState() => incomingRandomState = UnityEngine.Random.state;

        [TearDown]
        public void RestoreIncomingRandomState() => UnityEngine.Random.state = incomingRandomState;
    }
}
