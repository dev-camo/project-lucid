using System.Collections.Generic;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // SameSpaceObjectAvoidance target selection with genuine SmoothedTransformProxy
    // components, whose Awake caches their transforms. The synchronous test runs
    // no frame, so no LateUpdate, Time.deltaTime or curve is involved: the offset
    // stays zero and CalculateTarget snaps the rotation to its target.
    public sealed class SameSpaceObjectAvoidanceTargetTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

        // One neighbour at the origin, this proxy at +X, both facing +Z.
        // CalculateCircle: centre = neighbour, radius = 0 + m_SSOADistance (3).
        // CalculateRotation 0x06002302, x86-64 0x582210: rotation*forward, then
        // position - centre (subps at 0x5822ee), rotation*up, SignedVectorAngle
        // (0x582342) = Angle * sign(dot(other, cross(forward, up))) = -90.
        // CalculateTarget 0x06002300, x86-64 0x581bb0: zero Offset (< tolerance,
        // 0x581c01) snaps m_rotation to -90; direction = AngleAxis(-90, up) * forward
        // = -X; m_targetOffset (+0x78) = centre - position + direction * radius =
        // (-4, 0, 0) (0x581d85..0x581dde); the return registers hold the direction.
        // Catches returning the offset instead of the direction, and a
        // centre - position sign (direction +X, offset (2, 0, 0)).
        [Test]
        public void TargetUsesThePositionMinusCentreAngleAndReturnsTheRadialDirection()
        {
            var owned = new List<GameObject>();
            try
            {
                SmoothedTransformProxy self = Proxy(owned, "Lucid avoidance self proxy", Vector3.right);
                SmoothedTransformProxy neighbour = Proxy(owned, "Lucid avoidance neighbour proxy", Vector3.zero);
                var avoidanceObject = new GameObject("Lucid avoidance component");
                avoidanceObject.SetActive(false);
                owned.Add(avoidanceObject);
                SameSpaceObjectAvoidance avoidance = avoidanceObject.AddComponent<SameSpaceObjectAvoidance>();
                Set(avoidance, "m_proxy", self);
                Set(avoidance, "m_allConcurrentProxies", new List<SmoothedTransformProxy> { neighbour, self });
                Set(avoidance, "m_uniqueIndex", 1);
                // Genuine OnEnable allocates the working list and squared distance.
                avoidanceObject.SetActive(true);
                Assert.AreSame(self.transform, self.CachedTransform, "Proxy Awake cached its transform.");

                typeof(SameSpaceObjectAvoidance).GetMethod("UpdateTargetActors", Instance).Invoke(avoidance, new object[] { 1 });

                var selected = (List<SmoothedTransformProxy>)Get(avoidance, "m_ssoaProxyList");
                CollectionAssert.AreEqual(new[] { neighbour }, selected);
                Near(Vector3.zero, (Vector3)Get(avoidance, "m_centre"), "centre");
                Assert.AreEqual(3f, (float)Get(avoidance, "m_radius"), 1e-5f);
                Assert.AreEqual(-90f, (float)Get(avoidance, "m_rotation"), 1e-3f);
                Near(new Vector3(-4f, 0f, 0f), (Vector3)Get(avoidance, "m_targetOffset"), "target offset");
                Near(Vector3.zero, avoidance.Offset, "offset is only interpolated in LateUpdate");

                var direction = (Vector3)typeof(SameSpaceObjectAvoidance).GetMethod("CalculateTarget", Instance).Invoke(avoidance, null);
                Near(new Vector3(-1f, 0f, 0f), direction, "CalculateTarget returns the radial direction");
                Near(new Vector3(-4f, 0f, 0f), (Vector3)Get(avoidance, "m_targetOffset"), "target offset is recomputed unchanged");
            }
            finally
            {
                for (int i = owned.Count - 1; i >= 0; --i)
                    if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            }
        }

        private static SmoothedTransformProxy Proxy(List<GameObject> owned, string name, Vector3 position)
        {
            var value = new GameObject(name);
            owned.Add(value);
            value.transform.SetPositionAndRotation(position, Quaternion.identity);
            return value.AddComponent<SmoothedTransformProxy>();
        }

        private static void Set(SameSpaceObjectAvoidance avoidance, string name, object value) =>
            typeof(SameSpaceObjectAvoidance).GetField(name, Instance).SetValue(avoidance, value);

        private static object Get(SameSpaceObjectAvoidance avoidance, string name) =>
            typeof(SameSpaceObjectAvoidance).GetField(name, Instance).GetValue(avoidance);

        private static void Near(Vector3 expected, Vector3 actual, string label)
        {
            Assert.That((expected - actual).magnitude, Is.LessThan(1e-4f), label + ": expected " + expected + " but was " + actual);
        }
    }
}
