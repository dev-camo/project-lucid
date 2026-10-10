using NUnit.Framework;
using ProjectLucid.Verification;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class SequencedEffectTests
    {
        [Test]
        public void DefaultsAndNonPositiveDelaysKeepOriginalHooks()
        {
            SequencedEffectVerification.DefaultsAndNonPositiveDelaysKeepOriginalHooks();
        }

        [Test]
        public void InclusiveAndExclusiveGatesUseRealCurrentInput()
        {
            SequencedEffectVerification.InclusiveAndExclusiveGatesUseRealCurrentInput();
        }

        [Test]
        public void RejectedCallbackReentryAndFaultKeepLatestCompletion()
        {
            SequencedEffectVerification.RejectedCallbackReentryAndFaultKeepLatestCompletion();
        }
    }
}
