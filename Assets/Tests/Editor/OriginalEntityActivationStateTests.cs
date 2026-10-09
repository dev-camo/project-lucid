using NUnit.Framework;
using ProjectLucid.Verification;
namespace ProjectLucid.Tests
{
    public sealed class OriginalEntityActivationStateTests
    {
        [Test] public void ActivationTransitionOrderAndFirstTime() { Assert.AreEqual(20,EntityActivationStatePreservationVerification.TransitionOrderAndFirstTime()); }
        [Test] public void ActivationCallbackFaultAndLiveOppositeList() { Assert.AreEqual(18,EntityActivationStatePreservationVerification.CallbackFaultAndLiveOppositeList()); }
        [Test] public void ActivationPredicateShortCircuitAndLiveMutation() { Assert.AreEqual(16,EntityActivationStatePreservationVerification.PredicateShortCircuitAndLiveMutation()); }
        [Test] public void ActivationShutdownAndClearFaultPrefix() { Assert.AreEqual(16,EntityActivationStatePreservationVerification.ShutdownAndManagerClearFaultPrefix()); }
    }
}
