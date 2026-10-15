using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Non-null counterparts of the null-owner faults in RecoveredBehaviorTests
    // (InertSplinePrerequisiteVerification / KnotRuntimePrerequisiteVerification):
    // genuine InertSpline, InertSplineKnot, SplineKnot and RibbonSubKnot owners
    // with authored LUTs and transforms, so the native arithmetic actually runs.
    public sealed class OriginalSplineRuntimeLookupTests
    {
        private const float Tolerance = 1e-5f;
        private const BindingFlags Own = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static InertSplineKnot Knot(float[] lut)
        {
            var knot = new InertSplineKnot();
            typeof(InertSplineKnot).GetField("m_tLut", Own).SetValue(knot, lut);
            return knot;
        }

        private static void Near(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance), message + " (actual " + actual.ToString("F6") + ")");
        }

        // Original HLSplines.Runtime 0x06000483/0x06000484 (x86-64 0x1a8a8c0/0x1a8a910)
        // tail-call 0x060003de GetLinearRatioFromKnotT (x86-64 0x1a85cd0) and
        // 0x060003df GetKnotTFromLinearRatio (x86-64 0x1a86070). The native bodies:
        // fast path when LUT[0] <= t < LUT[1]; otherwise Array.BinarySearch with the
        // original comparer, exact hit -> index/count, miss -> lerp from ~index - 1
        // via 0x060003dd (x86-64 0x1a85a40) as (i + (t - LUT[i]) / (LUT[i+1] - LUT[i])) / count,
        // unclamped. The inverse truncates (cvttss2si) ratio*count and uses the
        // clamped Mathf.Lerp. 0x060001c6 GetLUTValue (x86-64 0x1a6b080) returns 1
        // at/after the end and 0 before the start. A non-uniform LUT distinguishes
        // using ~index instead of ~index - 1, floor instead of truncation, clamping
        // the extrapolated ratio, or reading another knot's table.
        [Test]
        public void InertRatioLookupsUseTheSelectedKnotLutSearchAndUnclampedLerp()
        {
            var owner = new GameObject("Project Lucid inert ratio fixture");
            try
            {
                owner.SetActive(false);
                var spline = owner.AddComponent<InertSpline>();
                var knots = new[] { Knot(new[] { 0f, .25f, .5f, .75f }), Knot(new[] { .2f, .3f, .5f, .8f }) };
                typeof(InertSpline).GetField("m_knots", Own).SetValue(spline, knots);

                (float t, float ratio)[] forward =
                {
                    (.25f, .125f),   // fast path inside the first LUT interval
                    (.2f, 0f),       // fast path lower bound is inclusive
                    (.3f, .25f),     // exact search hit
                    (.5f, .5f),      // exact search hit
                    (.65f, .625f),   // interior miss lerps from index 2
                    (.9f, .875f),    // last interval uses GetLUTValue(count) == 1
                    (1.4f, 1.5f),    // beyond the end extrapolates without clamping
                    (.1f, -.125f),   // before the start lerps from index -1 (value 0)
                };
                foreach (var row in forward)
                {
                    LinearRatio ratio = spline.GetLinearRatioFromKnotT(new KnotT(1, row.t));
                    Assert.That(ratio.KnotIndex, Is.EqualTo(1));
                    Assert.That(ratio.T, Is.EqualTo(row.ratio).Within(Tolerance), "KnotT " + row.t);
                }

                (float ratio, float t)[] inverse =
                {
                    (.625f, .65f),   // index 2, half way to LUT[3]
                    (.875f, .9f),    // index 3, half way to the implicit end value 1
                    (.25f, .3f),     // exact LUT entry
                    (.1f, .24f),     // index 0, fraction .4
                    (1.5f, 1f),      // both lookups past the end return 1
                    (-.125f, .2f),   // truncation keeps index 0; Lerp clamps -.5 to 0
                };
                foreach (var row in inverse)
                {
                    KnotT knotT = spline.GetKnotTFromLinearRatio(new LinearRatio(1, row.ratio));
                    Assert.That(knotT.KnotIndex, Is.EqualTo(1));
                    Assert.That(knotT.T, Is.EqualTo(row.t).Within(Tolerance), "LinearRatio " + row.ratio);
                }
            }
            finally { Object.DestroyImmediate(owner); }
        }

        // Original 0x060001c7 TransformPositionLocalToWorld (x86-64 0x1a6b160) and
        // 0x060001c8 TransformPositionWorldToLocal (x86-64 0x1a6b2e0) build
        // Matrix4x4.TRS(knot.Transform.Location, Orientation, Vector3.one) (the
        // static Vector3.one slot) and multiply, the latter through matrix.inverse.
        // SplineKnot 0x060002fa get_Transform (x86-64 0x1a75b90) reads localPosition/
        // localRotation; RibbonSubKnot 0x060002b8 (x86-64 0x1a73830) adds the centre
        // LocalOffset rotated by the parent's localRotation. Parent and knot scales
        // are therefore ignored; a TransformPoint/world-matrix rewrite would not be.
        [Test]
        public void KnotControlPointsUseUnitScaleLocalKnotMatrices()
        {
            var parent = new GameObject("Project Lucid knot matrix parent");
            var ribbonOwner = new GameObject("Project Lucid ribbon knot matrix fixture");
            try
            {
                parent.SetActive(false); ribbonOwner.SetActive(false);
                parent.transform.position = new Vector3(100f, 0f, 0f);
                parent.transform.localScale = new Vector3(5f, 5f, 5f);
                var child = new GameObject("Project Lucid spline knot matrix fixture");
                child.transform.SetParent(parent.transform, false);
                child.transform.localPosition = new Vector3(1f, 2f, 3f);
                child.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                child.transform.localScale = new Vector3(2f, 3f, 4f);
                var knot = child.AddComponent<SplineKnot>();
                // Constructor defaults are forward/back; Euler(0, 90, 0) maps +z to +x.
                Near(knot.WorldControlPointNext, new Vector3(2f, 2f, 3f), "next control uses the local unit-scale knot matrix");
                Near(knot.WorldControlPointPrev, new Vector3(0f, 2f, 3f), "previous control uses the same matrix");
                Near(knot.TangentNext, new Vector3(1f, 0f, 0f), "tangent subtracts the local knot location");

                ribbonOwner.transform.localPosition = new Vector3(1f, 2f, 3f);
                ribbonOwner.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                ribbonOwner.transform.localScale = new Vector3(2f, 3f, 4f);
                var ribbonKnot = ribbonOwner.AddComponent<RibbonKnot>();
                var sub = new RibbonSubKnot(RibbonAlignment.Center, 4);
                sub.Initialise(ribbonKnot);
                sub.LocalOffset = new Vector3(.5f, 0f, 0f);
                // Location = (1, 2, 3) + R * (.5, 0, 0) = (1, 2, 2.5).
                Near(sub.Transform.Location, new Vector3(1f, 2f, 2.5f), "centre sub-knot location includes its rotated offset");
                sub.WorldControlPointNext = new Vector3(2f, 2f, 2.5f);
                Near(sub.LocalControlPointNext, new Vector3(0f, 0f, 1f), "inverse unit-scale matrix stores the local control");
                Near(sub.WorldControlPointNext, new Vector3(2f, 2f, 2.5f), "forward matrix restores the authored world control");
                sub.WorldControlPointPrev = new Vector3(1f, 5f, 2.5f);
                Near(sub.LocalControlPointPrev, new Vector3(0f, 3f, 0f), "inverse ignores the owner's y scale");
            }
            finally
            {
                try { Object.DestroyImmediate(ribbonOwner); }
                finally { Object.DestroyImmediate(parent); }
            }
        }
    }
}
