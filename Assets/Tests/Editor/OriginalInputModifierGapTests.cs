using Hardlight;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Owned modifier assets only; no input system, control mapping or Time state is used.
    public sealed class OriginalInputModifierGapTests
    {
        // ModifierCoolDown..ctor (0x0600014b, x86-64 0x1a2aad0) stores 0x3e4ccccd (0.2f) at
        // +0x30; ModifierDeadZone..ctor (0x0600014f, x86-64 0x1a2ad90) stores the packed pair
        // 0.5f/0.4f at +0x1c. Two exact 0.1f steps reach 0.2f, so the cooldown completes on
        // the second step and the next activation passes. Fails if any default changes
        // (e.g. a 0.25f cooldown still suppresses the fourth call).
        [Test]
        public void ConstructorsKeepTheOriginalCooldownAndDeadZoneDefaults()
        {
            ModifierCoolDown cooldown = null;
            ModifierDeadZone deadZone = null;
            try
            {
                cooldown = ScriptableObject.CreateInstance<ModifierCoolDown>();
                deadZone = ScriptableObject.CreateInstance<ModifierDeadZone>();
                using (var serialized = new SerializedObject(cooldown))
                    Assert.That(serialized.FindProperty("m_cooldownTime").floatValue, Is.EqualTo(0.2f));
                using (var serialized = new SerializedObject(deadZone))
                {
                    Assert.That(serialized.FindProperty("m_openGateValue").floatValue, Is.EqualTo(0.5f));
                    Assert.That(serialized.FindProperty("m_closeGateValue").floatValue, Is.EqualTo(0.4f));
                    Assert.That(serialized.FindProperty("m_invertDeadZone").boolValue, Is.False);
                }

                Assert.That(0.1f + 0.1f, Is.EqualTo(0.2f), "the two steps sum exactly to the default");
                Assert.That(cooldown.Modify(1f, 0f), Is.EqualTo(1f), "activation passes");
                Assert.That(cooldown.Modify(1f, 0.1f), Is.EqualTo(0f), "still cooling below the default");
                Assert.That(cooldown.Modify(1f, 0.1f), Is.EqualTo(0f), "completion frame returns zero");
                Assert.That(cooldown.Modify(1f, 0f), Is.EqualTo(1f), "cooldown ended exactly at the default");
            }
            finally
            {
                try
                {
                    if (cooldown != null) Object.DestroyImmediate(cooldown);
                }
                finally
                {
                    if (deadZone != null) Object.DestroyImmediate(deadZone);
                }
            }
            LogAssert.NoUnexpectedReceived();
        }

        // ModifierCurve.Modify(Vector3) (0x06000002, x86-64 0x1a199c0) returns Vector3.zero
        // only while 0.0001f > x*x+y*y (ucomiss/jbe at 0x1a199de), keeping Z otherwise. XY
        // magnitudes 0.0099 and 0.0101 sit about 2% either side of the cutoff, independent of
        // float/double intermediate precision. Fails if the cutoff becomes 0.001f or 0.00001f.
        [Test]
        public void VectorCurveCutsOffBelowTheOriginalSquaredMagnitude()
        {
            global::ModifierCurve curve = null;
            try
            {
                curve = ScriptableObject.CreateInstance<global::ModifierCurve>();
                using (var serialized = new SerializedObject(curve))
                {
                    serialized.FindProperty("m_curve").animationCurveValue = AnimationCurve.Constant(0f, 1f, 0.5f);
                    Assert.That(serialized.ApplyModifiedPropertiesWithoutUndo(), Is.True);
                }
                Assert.That(curve.Modify(new Vector3(0.0099f, 0f, 5f), 0f), Is.EqualTo(Vector3.zero));
                Assert.That(curve.Modify(new Vector3(0f, -0.0099f, 5f), 0f), Is.EqualTo(Vector3.zero));
                Vector3 above = curve.Modify(new Vector3(0.0101f, 0f, 5f), 0f);
                Assert.That(above.x, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(above.y, Is.EqualTo(0f));
                Assert.That(above.z, Is.EqualTo(5f));
                Vector3 below = curve.Modify(new Vector3(0f, -0.0101f, -2f), 0f);
                Assert.That(below.y, Is.EqualTo(-0.5f).Within(0.0001f));
                Assert.That(below.z, Is.EqualTo(-2f));
            }
            finally
            {
                if (curve != null) Object.DestroyImmediate(curve);
            }
            LogAssert.NoUnexpectedReceived();
        }
    }
}
