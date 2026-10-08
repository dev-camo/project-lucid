using NUnit.Framework;
using ProjectLucid.Verification;

namespace ProjectLucid.Tests
{
    public sealed class OriginalDelayModifierPreservationTests
    {
        [Test] public void ScalarUsesOldIndependentKeyTimers()
        { Assert.That(OriginalDelayModifierVerification.RunScalar36(), Is.EqualTo(36)); }
        [Test] public void VectorUsesNewTimerAndResetPreservesFaultPrefix()
        { Assert.That(OriginalDelayModifierVerification.RunVectorReset24(), Is.EqualTo(24)); }
        [Test] public void OriginalUnityConstructorCreatesCache()
        { Assert.That(OriginalDelayModifierVerification.RunConstructionEngine8(), Is.EqualTo(8)); }
    }
}
