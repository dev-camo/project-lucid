using HardlightProject;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Original ProcessDecelerationCurve 0x06003902 (x86-64 0x6A8FC0). Below the
    // minimum, 0x6a9021 mulss deltaTime*Acceleration, 0x6a9027 addss adds
    // forwardSpeed, then 0x6a902b minss xmm2(minimum), xmm3(sum) stores
    // minimum < sum ? minimum : sum, i.e. Mathf.Min(minimum, sum).
    public sealed class CharacterMovementDecelerationCurveTests
    {
        // Fails against Mathf.Min(sum, minimum), which returns the finite minimum.
        [Test]
        public void AccelerationKeepsAnUnorderedSum()
        {
            Assert.IsTrue(float.IsNaN(Accelerate(float.NaN)), "minss returns its source operand when unordered.");
        }

        // The finite cases are order-insensitive controls for the clamp itself.
        [Test]
        public void AccelerationClampsAFiniteSumToTheMinimum()
        {
            Assert.AreEqual(1f, Accelerate(5f));
        }

        [Test]
        public void AccelerationKeepsAFiniteSumBelowTheMinimum()
        {
            Assert.AreEqual(0.25f, Accelerate(0.25f));
        }

        // forwardSpeed 0 against SpeedMin 1, deltaTime 1 and multiplier 1 takes
        // only the acceleration branch, so the curve is never evaluated.
        private static float Accelerate(float acceleration)
        {
            var motion = new CharacterAbilityDefinition.Motion { SpeedMin = 1f, Acceleration = acceleration };
            float forwardSpeed = 0f;
            CharacterMovementUtilities.ProcessDecelerationCurve(ref forwardSpeed, motion, new AnimationCurve(), 0f, 1f, 1f);
            return forwardSpeed;
        }
    }
}
