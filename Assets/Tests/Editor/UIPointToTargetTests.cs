using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIPointToTarget (Game.Runtime 0x02000849, x86-64 0x60B670..0x60B720) as a real component on an inactive owned host. Update is
    // called directly, as Edit Mode sends none. The expected angles come from the native body: the planar direction target -
    // position (the Z component is never read), negated for a target behind the camera, gives eulerAngles.z = atan2(-x, y) *
    // 57.29578 (the constants at 0x29A6CC0 and 0x29A6C14) with X and Y zero. atan2f may differ from Mono's by an ulp, so the
    // angles are compared within 1e-3 degrees.
    public sealed class UIPointToTargetTests
    {
        private const float AngleTolerance = 1e-3f;

        // dump.cs TypeDefIndex 2120 and the DummyDll rows (fields 0x04001fb5, 0x04001fb6).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIPointToTarget), 0x00100001,
                new (string, int)[] { ("m_targetPosition", 0x0001), ("m_targetBehindCamera", 0x0001) },
                new (string, int)[] { ("SetTarget", 0x0086), ("SetTargetBehindCamera", 0x0086), ("Update", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIPointToTarget), 1, 2);
            Assert.That(typeof(UIPointToTarget).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
        }

        // The setters at 0x60B670 and 0x60B680 are plain stores.
        [Test]
        public void TheSettersStoreTheirArguments()
        {
            Owned.Run(owned =>
            {
                var pointer = owned.Host("Lucid pointer", false).AddComponent<UIPointToTarget>();

                pointer.SetTarget(new Vector3(1f, 2f, 3f));
                pointer.SetTargetBehindCamera(true);

                Assert.That(Get<Vector3>(pointer, "m_targetPosition"), Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(Get<bool>(pointer, "m_targetBehindCamera"), Is.True);
            });
        }

        // Hand-derived from the native formula: the four axis directions, and a diagonal. The expected z is the Unity euler angle,
        // which is reported in [0, 360).
        [TestCase(0f, 1f, false, 0f, TestName = "ATargetStraightAboveGivesZeroDegrees")]
        [TestCase(1f, 0f, false, 270f, TestName = "ATargetToTheRightTurnsClockwise")]
        [TestCase(-1f, 0f, false, 90f, TestName = "ATargetToTheLeftTurnsAnticlockwise")]
        [TestCase(0f, -1f, false, 180f, TestName = "ATargetStraightBelowGivesHalfATurn")]
        [TestCase(1f, 1f, false, 315f, TestName = "AnUpperRightTargetGivesFortyFiveDegreesClockwise")]
        [TestCase(0f, 1f, true, 180f, TestName = "ABehindCameraTargetStraightAboveFlipsTheDirection")]
        [TestCase(1f, 0f, true, 90f, TestName = "ABehindCameraTargetToTheRightFlipsTheDirection")]
        public void UpdateTurnsTheArrowAboutZTowardsTheTarget(float x, float y, bool behindCamera, float expectedZ)
        {
            Owned.Run(owned =>
            {
                var pointer = owned.Host("Lucid pointer", false).AddComponent<UIPointToTarget>();
                pointer.SetTarget(new Vector3(x, y, 7f));
                pointer.SetTargetBehindCamera(behindCamera);

                Call(pointer, "Update");

                Vector3 euler = pointer.transform.eulerAngles;
                Assert.That(euler.z, Is.EqualTo(expectedZ).Within(AngleTolerance));
                Assert.That(euler.x, Is.EqualTo(0f).Within(AngleTolerance));
                Assert.That(euler.y, Is.EqualTo(0f).Within(AngleTolerance));
            });
        }

        // 0x60B6A1..0x60B6B6: the direction is measured from the transform's world position, and the euler angles are written whole,
        // so a rotation about the other axes is replaced.
        [Test]
        public void UpdateMeasuresFromTheWorldPositionAndResetsTheOtherAxes()
        {
            Owned.Run(owned =>
            {
                var pointer = owned.Host("Lucid pointer", false).AddComponent<UIPointToTarget>();
                pointer.transform.position = new Vector3(2f, 3f, 4f);
                pointer.transform.eulerAngles = new Vector3(30f, 40f, 50f);
                pointer.SetTarget(new Vector3(2f, 6f, -9f));

                Call(pointer, "Update");

                Vector3 euler = pointer.transform.eulerAngles;
                Assert.That(euler.z, Is.EqualTo(0f).Within(AngleTolerance), "directly above the position");
                Assert.That(euler.x, Is.EqualTo(0f).Within(AngleTolerance));
                Assert.That(euler.y, Is.EqualTo(0f).Within(AngleTolerance));
            });
        }
    }
}
