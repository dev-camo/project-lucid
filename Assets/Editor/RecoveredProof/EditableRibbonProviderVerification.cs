using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Editor
{
    // Checks knot initialization and edge rules separately from Unity component geometry.
    public static class EditableRibbonProviderVerification
    {
        private static int s_checks;
        private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); ++s_checks; }
        private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly) ?? throw new MissingFieldException(type.FullName, name);
        private static object Get(object value, string name) => Field(value.GetType(), name).GetValue(value);
        private static void Set(object value, string name, object data) => Field(value.GetType(), name).SetValue(value, data);
        private static void Throws<T>(Action action, string name) where T : Exception
        {
            try { action(); } catch (T exception) { Check(exception.GetType() == typeof(T), name); return; }
            throw new InvalidOperationException(name + " did not throw");
        }
        private static bool Near(float first, float second) => Math.Abs(first - second) < .002f;
        private static bool Same(Vector3 first, Vector3 second) => Near(first.x, second.x) && Near(first.y, second.y) && Near(first.z, second.z);

        public static int RunManaged()
        {
            s_checks = 0;
            foreach (RibbonAlignment alignment in new[] { RibbonAlignment.Center, RibbonAlignment.Left, RibbonAlignment.Right, (RibbonAlignment)47 })
            {
                RibbonSubKnot knot = new RibbonSubKnot(alignment, 4);
                Check(knot.Alignment == alignment, "constructor keeps actual alignment without validation");
                Check(knot.Parent == null, "new value owner field is null");
                Check(knot.TLUT.Length == 4 && knot.GetLUTLength() == 4, "constructor allocates count, not count+1");
                Check(knot.Length == 0f && float.IsPositiveInfinity(knot.LengthInverse), "zero native reciprocal remains infinite");
                Check(knot.IsLengthDirty(), "new knot starts dirty");
                Check(Same((Vector3)Get(knot, "m_localOffset"), Vector3.zero), "actual offset initializer");
                Check(Same((Vector3)Get(knot, "m_localControlPointNext"), Vector3.forward), "actual forward control initializer");
                Check(Same((Vector3)Get(knot, "m_localControlPointPrev"), -Vector3.forward), "actual negative forward initializer");
                Check(knot.GetLUTValue(-1) == 0f && knot.GetLUTValue(4) == 1f, "original LUT boundary order");
                knot.TLUT[1] = .75f;
                Check(knot.GetLUTValue(1) == .75f, "actual stored LUT value");
                float[] saved = knot.TLUT;
                knot.Initialise(null);
                Check(ReferenceEquals(saved, knot.TLUT), "parent-only initialize does not resize");
                knot.Initialise(null, 4);
                Check(knot.GetLUTLength() == 5 && !ReferenceEquals(saved, knot.TLUT), "count initialization allocates count+1 unconditionally");
                saved = knot.TLUT;
                knot.Initialise(null, 4);
                Check(!ReferenceEquals(saved, knot.TLUT), "same count still replaces LUT storage");
                knot.TLUT = null;
                Check(knot.GetLUTValue(20) == 0f && knot.GetLUTValue(-4) == 0f, "missing LUT returns zero before bounds branches");
                Throws<NullReferenceException>(() => knot.GetLUTLength(), "missing LUT length faults");
                knot.TLUT = new float[0];
                Check(knot.GetLUTValue(0) == 1f && knot.GetLUTValue(-1) == 0f, "zero-length LUT checks upper bound before negative");
                Set(knot, "m_length", -2f);
                Check(knot.LengthInverse == -.5f, "negative length reciprocal retained");
                Set(knot, "m_length", float.NaN);
                Check(float.IsNaN(knot.LengthInverse), "NaN reciprocal retained");
                Set(knot, "m_lengthDirty", false);
                knot.SetLengthDirty();
                Check(knot.IsLengthDirty(), "dirty store is unconditional");
                knot.LocalOffset = new Vector3(2f, 3f, 4f);
                Check(Same((Vector3)Get(knot, "m_localOffset"), new Vector3(2f, 3f, 4f)), "setter stores before any parent dependency");
                knot.LocalControlPointNext = new Vector3(5f, 6f, 7f);
                knot.LocalControlPointPrev = new Vector3(8f, 9f, 10f);
                Check(Same((Vector3)Get(knot, "m_localControlPointNext"), new Vector3(5f, 6f, 7f)), "next setter stores exact vector");
                Check(Same((Vector3)Get(knot, "m_localControlPointPrev"), new Vector3(8f, 9f, 10f)), "previous setter stores exact vector");
                Throws<NullReferenceException>(() => { var value = knot.LocalOffset; }, "center and invalid alignment still query parent first");
                Throws<NullReferenceException>(() => { var value = knot.LocalControlPointNext; }, "next control queries parent before center gate");
                Throws<NullReferenceException>(() => { var value = knot.Metadata; }, "metadata real parent fault");
            }
            foreach (float distance in new[] { 3f, -2f, float.NaN })
            {
                RaycastHit output = default;
                try
                {
                    RibbonUtilities.TryGetRaycastHit(null, default, distance, null, out output);
                    throw new InvalidOperationException("missing real ribbon did not fault");
                }
                catch (NullReferenceException)
                {
                    Check(float.IsNaN(distance) ? float.IsNaN(output.distance) : output.distance == distance + 1f,
                        "original ray output is published before the missing ribbon count fault");
                }
            }
            RibbonSubKnot countOnly = new RibbonSubKnot(0);
            Check(countOnly.Alignment == RibbonAlignment.Center && countOnly.TLUT.Length == 0, "single-argument constructor defaults center");
            Throws<OverflowException>(() => new RibbonSubKnot(-1), "negative CLR array count fault");
            float[] beforeFailure = new float[] { .2f };
            countOnly.TLUT = beforeFailure;
            // Unity's Editor allocator and .NET reject this negative count with OverflowException.
            // Standalone Unity Mono reports OutOfMemoryException for int.MinValue.
#if UNITY_EDITOR
            Throws<OverflowException>(() => countOnly.Initialise(null, int.MaxValue), "Editor unchecked count+1 allocation fault");
#else
            if (Type.GetType("Mono.Runtime") != null)
                Throws<OutOfMemoryException>(() => countOnly.Initialise(null, int.MaxValue), "pinned Mono unchecked count+1 allocation fault");
            else
                Throws<OverflowException>(() => countOnly.Initialise(null, int.MaxValue), "NET10 unchecked count+1 allocation fault");
#endif
            Check(ReferenceEquals(beforeFailure, countOnly.TLUT), "failed allocation preserves old LUT");
            countOnly.Initialise(null, -1);
            Check(countOnly.TLUT.Length == 0, "initialize -1 becomes zero count without clamping");

            foreach (RibbonAlignment alignment in new[] { RibbonAlignment.Left, RibbonAlignment.Center, RibbonAlignment.Right, (RibbonAlignment)19 })
            {
                RibbonSubSpline spline = new RibbonSubSpline(alignment);
                Check(spline.Alignment == alignment, "sub-spline keeps exact alignment");
                Check(spline.Length == 0f && float.IsPositiveInfinity(spline.LengthInverse), "sub-spline zero reciprocal");
                Check(spline.LUTCount == 0, "sub-spline auto backing default");
                Set(spline, "<LUTCount>k__BackingField", 31);
                Throws<NullReferenceException>(() => spline.Initialise(null), "sub-spline parent store then actual owner LUT fault");
                Check(spline.LUTCount == 31, "failed parent LUT query retains previous count");
                Throws<NullReferenceException>(() => { var value = spline.Metadata; }, "sub-spline actual metadata parent fault");
                Throws<NullReferenceException>(() => { var value = spline.KnotCount; }, "sub-spline count parent fault");
                Throws<NullReferenceException>(() => spline.GetKnot(0), "sub-spline count queried before index");
                Set(spline, "m_length", -4f);
                Check(spline.LengthInverse == -.25f, "sub-spline negative reciprocal");
                Set(spline, "m_length", float.NaN);
                Check(float.IsNaN(spline.LengthInverse), "sub-spline NaN reciprocal");
            }
            Check(typeof(RibbonKnot).IsSerializable && typeof(RibbonSubKnot).IsSerializable && typeof(RibbonSubSpline).IsSerializable, "three original Serializable type flags");
            Check(Field(typeof(RibbonSubSpline), "m_length").IsFamily, "original protected length field");
            Check(typeof(RibbonKnot).GetMethod("GetSubKnots", new[] { typeof(RibbonSubKnot[]).MakeByRefType() }).GetParameters()[0].IsOut, "original out array parameter");
            Check((bool)typeof(EditableRibbon).GetMethod("GetBoundingBox").GetParameters()[0].DefaultValue == false, "original optional false world bounds default");
            Check((bool)typeof(RibbonSubSpline).GetMethod("RecalculateLength").GetParameters()[0].DefaultValue == false, "original optional false force default");
            return s_checks;
        }

        // Exercises the original component with actual Unity transforms,
        // real spline callbacks and genuine triangle solver, never a substitute IRibbon implementation.
        public static int RunEngine()
        {
            s_checks = 0;
            GameObject ownerObject = null;
            int oldOverride = EditableRibbon.s_lutCountOverride;
            Type sharedRuntime = typeof(SplineLocation).Assembly.GetType("Hardlight.SplineRuntimeComponent", true);
            float[] sharedLut = (float[])sharedRuntime.GetField("s_lutCache", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            float[] oldLut = (float[])sharedLut.Clone();
            try
            {
                ownerObject = new GameObject("Project Lucid original EditableRibbon verification");
                EditableRibbon ribbon = ownerObject.AddComponent<EditableRibbon>();
                Check(ribbon.Knots != null && ribbon.KnotCount == 0 && ribbon.LUTCount == 10, "native owner list/count/LUT defaults");
                Check(Near(ribbon.GetSplineOffset(RibbonAlignment.Left), -1f) && Near(ribbon.GetSplineOffset(RibbonAlignment.Right), 1f), "native paired widths one");
                Check(ribbon.GetSplineType((RibbonAlignment)27) == SplineType.CatmullRom, "invalid enum still uses side type");
                Check(ribbon.GetKnot(-90, (RibbonAlignment)27) == null && ribbon.GetHeadKnot((RibbonAlignment)27) == null, "empty knot guard precedes invalid alignment");
                Throws<ArgumentOutOfRangeException>(() => ribbon.GetSubSpline((RibbonAlignment)27), "actual invalid sub-spline alignment");
                Throws<ArgumentOutOfRangeException>(() => ribbon.GetSplineOffset((RibbonAlignment)27), "actual invalid width alignment");
                Check(ribbon.GetSubSpline(RibbonAlignment.Center).LUTCount == 0, "native new sub-spline backing count zero");
                EditableRibbon.s_lutCountOverride = 17;
                ribbon.Initialise();
                Check(ribbon.LUTCount == 17, "positive global override updates owner after sub-splines");
                Check(ribbon.GetSubSpline(RibbonAlignment.Center).LUTCount == 10 && ribbon.GetSubSpline(RibbonAlignment.Left).LUTCount == 10 && ribbon.GetSubSpline(RibbonAlignment.Right).LUTCount == 10, "three captured counts precede global override");
                EditableRibbon.s_lutCountOverride = 0;
                Set(ribbon, "m_lutCount", 10);
                Set(ribbon, "m_knots", null);
                Check(ribbon.KnotCount == 0, "count null list fallback is zero");
                Throws<NullReferenceException>(() => ribbon.GetBackKnot(RibbonAlignment.Center), "back count has no null-list fallback");
                Throws<NullReferenceException>(() => ribbon.Initialise(), "initialize list loop faults after sub-spline parents");
                Set(ribbon, "m_knots", new List<RibbonKnot>());

                RibbonKnot first = CreateKnot(ownerObject.transform, "first", Vector3.zero);
                RibbonKnot last = CreateKnot(ownerObject.transform, "last", new Vector3(0f, 0f, 10f));
                ribbon.Knots.Add(first);
                ribbon.Knots.Add(last);
                ribbon.Initialise();
                Check(first.IsInitialised() && last.IsInitialised(), "real three-parent initialization");
                Check(ReferenceEquals(first.Ribbon, ribbon) && ReferenceEquals(last.Ribbon, ribbon), "genuine parent transform cache resolves owner");
                Check(ribbon.ContainsKnot(first) && ribbon.GetKnotIndex(last) == 1, "genuine runtime knot index callbacks");
                Check(ReferenceEquals(ribbon.GetHeadKnot(RibbonAlignment.Left), first[RibbonAlignment.Left]) && ReferenceEquals(ribbon.GetBackKnot(RibbonAlignment.Right), last[RibbonAlignment.Right]), "head/back retain requested side");
                Check(ribbon.GetKnotBefore(first) == null && ribbon.GetKnotAfter(last) == null && ReferenceEquals(ribbon.GetKnotAfter(first), last), "actual neighboring knots");
                RibbonSubKnot[] cache;
                first.GetSubKnots(out cache);
                Check(cache.Length == 3 && ReferenceEquals(cache[0], first[RibbonAlignment.Center]) && ReferenceEquals(cache[1], first[RibbonAlignment.Right]) && ReferenceEquals(cache[2], first[RibbonAlignment.Left]), "native cached center/right/left order");
                RibbonSubKnot main, left, right;
                first.GetSubKnots(out main, out left, out right);
                Check(ReferenceEquals(main, cache[0]) && ReferenceEquals(left, cache[2]) && ReferenceEquals(right, cache[1]), "out overload has main/left/right ordering");
                main.TLUT = new float[] { .125f };
                first.Initialise(30);
                Check(main.TLUT.Length == 1 && main.TLUT[0] == .125f, "existing knot is retained without LUT repair");
                main.TLUT = new float[10];
                main.LocalControlPointNext = Vector3.forward * (10f / 3f);
                last[RibbonAlignment.Center].LocalControlPointPrev = -Vector3.forward * (10f / 3f);
                Field(typeof(RibbonMonoBase), "m_centerSplineType").SetValue(ribbon, SplineType.Bezier);
                Field(typeof(RibbonMonoBase), "m_sideSplineType").SetValue(ribbon, SplineType.Bezier);
                ribbon.RecalculateLength(true);
                Check(Near(ribbon.Length, 10f) && Near(ribbon.LengthInverse, .1f), "native straight Bezier center length");
                Check(Near(left.Length, 10f) && Near(right.Length, 10f), "real side lengths use main handles");
                Check(last[RibbonAlignment.Center].Length == 0f && float.IsPositiveInfinity(last[RibbonAlignment.Center].LengthInverse), "last segment remains native zero reciprocal");
                Check(!main.IsLengthDirty() && !left.IsLengthDirty() && !right.IsLengthDirty(), "length callbacks clear all dirty bits");
                Check(Same(main.Transform.Location, Vector3.zero) && Same(left.Transform.Location, Vector3.left) && Same(right.Transform.Location, Vector3.right), "real center/side local positions");
                Check(Same(main.WorldControlPointNext, Vector3.forward * (10f / 3f)), "real matrix-based control position");
                Check(Same(main.TangentNext, Vector3.forward * (10f / 3f)), "control callback before second transform tangent");
                Check(Same(ribbon.GetLocalPosFromKnotT(new KnotT(0, .5f), RibbonAlignment.Center), Vector3.forward * 5f), "real cubic point at midpoint");
                Bounds bounds = ribbon.GetBoundingBox(false);
                Check(Same(bounds.center, Vector3.forward * 5f) && Same(bounds.size, new Vector3(2.1f, .1f, 10.1f)), "native real bounds expansion");
                RaycastHit hit;
                Check(ribbon.TryGetRaycastHit(new Ray(new Vector3(.25f, 3f, 5f), Vector3.down), 4f, out hit) && Near(hit.distance, 3f) && Same(hit.point, new Vector3(.25f, 0f, 5f)), "genuine four-triangle surface hit");
                Check(!ribbon.TryGetRaycastHit(new Ray(new Vector3(.25f, 3f, 5f), Vector3.down), 3f, out hit) && Near(hit.distance, 4f), "distance tie is rejected and initialized miss retained");
                Check(!ribbon.TryGetRaycastHit(new Ray(new Vector3(.25f, -3f, 5f), Vector3.up), 4f, out hit), "native one-sided winding");
                int callbacks = 0;
                Action second = () => ++callbacks;
                Action firstCallback = () => { ++callbacks; ribbon.OnRibbonKnotsUpdated -= second; };
                ribbon.OnRibbonKnotsUpdated += firstCallback;
                ribbon.OnRibbonKnotsUpdated += second;
                ribbon.BroadcastKnotsUpdated();
                Check(callbacks == 2 && ribbon.GetTimestamp().Kind == DateTimeKind.Local, "captured multicast callback includes removed listener and timestamp is local");
                ribbon.BroadcastKnotsUpdated();
                Check(callbacks == 3, "subsequent event sees callback removal");
                IRibbon notified = ribbon;
                ribbon.OnRibbonTypeChanged += value => notified = value;
                ribbon.UpdateRibbonType(null);
                Check(notified == null && ribbon.GetTimestamp().Kind == DateTimeKind.Local, "type event accepts null after timestamp publication");
                DateTime timestamp = ribbon.GetTimestamp();
                ownerObject.SetActive(false);
                main.SetLengthDirty();
                first.KnotUpdated();
                Check(main.IsLengthDirty() && ribbon.GetTimestamp() == timestamp, "inactive knot update clears sorting but avoids recompute/broadcast");
                Check(Field(typeof(RibbonMonoBase), "m_knotBounds").GetValue(ribbon) == null && Field(typeof(RibbonMonoBase), "m_sortedKnots").GetValue(ribbon) == null, "original cache clearing occurs on inactive owner");
                ownerObject.SetActive(true);
                ownerObject.transform.position = new Vector3(3f, 4f, 5f);
                ownerObject.transform.localScale = new Vector3(2f, 1f, 2f);
                Check(Same(RibbonUtilities.GetSubKnotWorldPosition(RibbonAlignment.Right, ribbon, 1, ownerObject.transform), new Vector3(5f, 4f, 25f)), "real utility applies owner matrix after actual knot transform");
                // Original length sampling uses 2047 intervals, while the cached fraction
                // divides the selected sample index by 2048. Preserve that quantization.
                Vector3 worldPoint = ribbon.GetWorldTransformFromRibbonT(.5f).Location;
                Check(main.TLUT[5] == .49951171875f && Same(worldPoint, new Vector3(3f, 4f, 14.990234375f)),
                    "world transform retains original LUT quantization and owner point matrix");
                return s_checks;
            }
            finally
            {
                EditableRibbon.s_lutCountOverride = oldOverride;
                try
                {
                    if (ownerObject != null) UnityEngine.Object.DestroyImmediate(ownerObject);
                }
                finally
                {
                    Array.Copy(oldLut, sharedLut, oldLut.Length);
                }
            }
        }
        private static RibbonKnot CreateKnot(Transform parent, string name, Vector3 position)
        {
            GameObject value = new GameObject(name);
            value.transform.SetParent(parent, false);
            value.transform.localPosition = position;
            return value.AddComponent<RibbonKnot>();
        }
    }
}
