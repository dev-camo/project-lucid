using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectLucid.Editor
{
    // Owned inactive ribbon; its knots are genuine components that never run lifecycle.
    public sealed class OriginalSplineGapTests
    {
        // RibbonRuntimeComponent.IsEndEdge (0x060001bc, x86-64 0x1a696c0): for a nonzero knot
        // index it queries KnotCount, compares KnotCount - 2 with the index (0x1a69776) and
        // T >= 0.999f with ucomiss/setae (0x1a69783). With three knots only index 1 is the end
        // edge. Fails under `T > 0.999f`, `KnotCount - 1 == index`, or a T-only test.
        [Test]
        public void LastBlockEndEdgeUsesTheRealKnotCountAndInclusiveTolerance()
        {
            GameObject owner = null;
            try
            {
                owner = new GameObject("Owned inactive ribbon end-edge case");
                owner.SetActive(false);
                EditableRibbon ribbon = owner.AddComponent<EditableRibbon>();
                for (int i = 0; i < 3; i++)
                {
                    var knot = new GameObject("Owned knot " + i);
                    knot.transform.SetParent(owner.transform, false);
                    knot.transform.localPosition = new Vector3(0f, 0f, 5f * i);
                    ribbon.Knots.Add(knot.AddComponent<RibbonKnot>());
                }
                Assert.That(ribbon.KnotCount, Is.EqualTo(3));
                MethodInfo edge = typeof(SplineLocation).Assembly.GetType("Hardlight.RibbonRuntimeComponent", true)
                    .GetMethod("IsEndEdge", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(edge, Is.Not.Null);
                bool IsEndEdge(int index, float t) => (bool)edge.Invoke(null, new object[] { ribbon, new KnotT(index, t) });

                Assert.That(IsEndEdge(1, 0.999f), Is.True, "tolerance is inclusive");
                Assert.That(IsEndEdge(1, 1f), Is.True);
                Assert.That(IsEndEdge(1, 0.9989f), Is.False);
                Assert.That(IsEndEdge(1, float.NaN), Is.False, "unordered T is not an end edge");
                Assert.That(IsEndEdge(2, 1f), Is.False, "index past the last block");
                Assert.That(IsEndEdge(0, 1f), Is.False, "first block compares only its start");
                Assert.That(IsEndEdge(0, 0.001f), Is.True);
                Assert.That(owner.activeInHierarchy, Is.False);
            }
            finally
            {
                if (owner != null) Object.DestroyImmediate(owner);
            }
            LogAssert.NoUnexpectedReceived();
        }
    }
}
