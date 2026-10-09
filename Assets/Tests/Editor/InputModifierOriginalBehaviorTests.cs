using NUnit.Framework;
using ProjectLucid.Verification;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests.EditMode
{
    // Controlled original modifier checks; each helper counts completed assertions.
    public sealed class InputModifierOriginalBehaviorTests
    {
        [Test]
        public void DeadZoneHysteresisAndRadialCacheResetRemainOriginal()
        {
            Assert.That(InputModifierOriginalBehaviorVerification.VerifyDeadZones(), Is.EqualTo(16));
            LogAssert.NoUnexpectedReceived();
        }
        [Test]
        public void CooldownScalarVectorTimingAndDebounceInputStateRemainOriginal()
        {
            Assert.That(InputModifierOriginalBehaviorVerification.VerifyCooldownAndDebounce(), Is.EqualTo(18));
            LogAssert.NoUnexpectedReceived();
        }
        [Test]
        public void CurveXYSignZAndScalarModifierBranchesRemainOriginal()
        {
            Assert.That(InputModifierOriginalBehaviorVerification.VerifyCurvesAndScalarLeaves(), Is.EqualTo(18));
            LogAssert.NoUnexpectedReceived();
        }
        [Test]
        public void RepeatUsesRealPerInputStateAndUnityTimeWithoutRepairingScalarFault()
        {
            Assert.That(InputModifierOriginalBehaviorVerification.VerifyRepeat(), Is.EqualTo(14));
            LogAssert.NoUnexpectedReceived();
        }
        [Test]
        public void TriggerRegistersAndCallsBackBeforeDownAndResetStateStores()
        {
            Assert.That(InputModifierOriginalBehaviorVerification.VerifyTriggerOrderAndReset(), Is.EqualTo(7));
            LogAssert.NoUnexpectedReceived();
        }
        [Test]
        public void TriggerFaultsAndHeldCallbacksPreserveOriginalStateAndRegistration()
        {
            Assert.That(InputModifierOriginalBehaviorVerification.VerifyTriggerFaultsAndHeld(), Is.EqualTo(15));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
