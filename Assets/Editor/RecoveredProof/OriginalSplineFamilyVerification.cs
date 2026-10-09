using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Verification
{
    // Engine cases require an empty, quiescent Unity test context and own every created object.
    // Shared curve-sampling storage is restored in finally; compilation proves no Engine behavior.
    public static class OriginalSplineFamilyVerification
    {
        private sealed class Checks
        {
            public int Count;
            public void Is(bool condition, string label)
            {
                ++Count;
                if (!condition) throw new InvalidOperationException(label);
            }
            public void Near(float actual, float expected, string label, float tolerance = .002f)
                => Is(!float.IsNaN(actual) && Math.Abs(actual - expected) <= tolerance, label);
            public void Vector(Vector3 actual, Vector3 expected, string label, float tolerance = .002f)
                => Is(!float.IsNaN(actual.x) && !float.IsNaN(actual.y) && !float.IsNaN(actual.z)
                    && Math.Abs(actual.x - expected.x) <= tolerance
                    && Math.Abs(actual.y - expected.y) <= tolerance
                    && Math.Abs(actual.z - expected.z) <= tolerance, label);
            public void Throws<T>(Action action, string label) where T : Exception
            {
                Exception observed = null;
                try { action(); } catch (Exception e) { observed = e; }
                Is(observed != null && observed.GetType() == typeof(T), label);
            }
            public void SameFault(Action action, Exception expected, string label)
            {
                Exception observed = null;
                try { action(); } catch (Exception e) { observed = e; }
                Is(ReferenceEquals(observed, expected), label);
            }
        }

        private static FieldInfo Field(Type type, string name)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                FieldInfo field = t.GetField(name, BindingFlags.Instance | BindingFlags.Static
                    | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new MissingFieldException(type.FullName, name);
        }
        private static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
        private static T Get<T>(object target, string name) => (T)Field(target.GetType(), name).GetValue(target);
        private static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        private static bool SameBits(float[] left, float[] right)
        {
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; ++i) if (Bits(left[i]) != Bits(right[i])) return false;
            return true;
        }

        private sealed class SamplingCache : IDisposable
        {
            private readonly FieldInfo m_field;
            public readonly float[] Original;
            public readonly float[] Before;
            public SamplingCache()
            {
                Type runtime = typeof(EditableSpline).Assembly.GetType("Hardlight.SplineRuntimeComponent", true);
                m_field = Field(runtime, "s_lutCache");
                Original = (float[])m_field.GetValue(null);
                Before = (float[])Original.Clone();
            }
            public bool SameReference => ReferenceEquals(m_field.GetValue(null), Original);
            public void Dispose()
            {
                if (!SameReference) throw new InvalidOperationException("original sampling cache reference changed");
                Array.Copy(Before, Original, Before.Length);
            }
        }

        private sealed class OwnedObjects : IDisposable
        {
            private readonly List<GameObject> m_objects = new List<GameObject>();
            private readonly SamplingCache m_cache = new SamplingCache();
            public GameObject Create(string name)
            {
                GameObject value = new GameObject(name);
                m_objects.Add(value);
                return value;
            }
            public T Component<T>(string name) where T : Component => Create(name).AddComponent<T>();
            public void Dispose()
            {
                try
                {
                    for (int i = m_objects.Count - 1; i >= 0; --i)
                        if (m_objects[i] != null) UnityEngine.Object.DestroyImmediate(m_objects[i]);
                }
                finally { m_cache.Dispose(); }
            }
        }

        public static int PlainKnotFieldsAndLUT()
        {
            Checks c = new Checks();
            InertSplineKnot inert = new InertSplineKnot();
            SegmentedSplineKnot segmented = new SegmentedSplineKnot();
            c.Is(inert.Metadata == null && inert.TLUT == null, "inert reference defaults");
            c.Is(Bits(inert.Length) == 0 && Bits(inert.LengthInverse) == 0, "inert stored zero lengths");
            c.Vector(inert.Transform.Location, Vector3.zero, "inert transform default");
            c.Is(inert.Transform.Orientation.x == 0 && inert.Transform.Orientation.y == 0
                && inert.Transform.Orientation.z == 0 && inert.Transform.Orientation.w == 0, "inert uninitialised rotation");
            c.Is(segmented.Metadata == null && segmented.SegmentSplinePoints == null, "segmented reference defaults");
            c.Is(Bits(segmented.Length) == 0 && Bits(segmented.LengthInverse) == 0, "segmented stored zero lengths");
            c.Vector(segmented.Transform.Location, Vector3.zero, "segmented transform default");
            Vector3 localNext = new Vector3(1, 2, 3), localPrev = new Vector3(-4, 5, -6);
            Vector3 worldNext = new Vector3(11, 12, 13), worldPrev = new Vector3(14, 15, 16);
            foreach (object knot in new object[] { inert, segmented })
            {
                Set(knot, "m_localControlPointNext", localNext); Set(knot, "m_localControlPointPrev", localPrev);
                Set(knot, "m_worldControlPointNext", worldNext); Set(knot, "m_worldControlPointPrev", worldPrev);
                Set(knot, "m_tangentNext", new Vector3(7, 8, 9)); Set(knot, "m_tangentPrev", new Vector3(-7, -8, -9));
            }
            c.Vector(inert.LocalControlPointNext, localNext, "inert local next");
            c.Vector(inert.LocalControlPointPrev, localPrev, "inert local previous");
            c.Vector(inert.WorldControlPointNext, localNext, "inert original world getter reads local next");
            c.Vector(inert.WorldControlPointPrev, localPrev, "inert original world getter reads local previous");
            c.Vector(segmented.LocalControlPointNext, localNext, "segmented local next");
            c.Vector(segmented.LocalControlPointPrev, localPrev, "segmented local previous");
            c.Vector(segmented.WorldControlPointNext, worldNext, "segmented original world field next");
            c.Vector(segmented.WorldControlPointPrev, worldPrev, "segmented original world field previous");
            c.Vector(inert.TangentNext, new Vector3(7, 8, 9), "inert stored next tangent");
            c.Vector(segmented.TangentPrev, new Vector3(-7, -8, -9), "segmented stored previous tangent");
            MetadataGroups metadata = new MetadataGroups();
            Set(inert, "m_metadata", metadata); segmented.m_metadata = metadata;
            c.Is(ReferenceEquals(inert.Metadata, metadata) && ReferenceEquals(segmented.Metadata, metadata), "metadata references");
            Quaternion unnormalised = new Quaternion(1, 2, 3, 4);
            inert.SetTransform(localNext, unnormalised);
            c.Vector(inert.Transform.Location, localNext, "SetTransform location");
            c.Is(inert.Transform.Orientation.x == 1 && inert.Transform.Orientation.y == 2
                && inert.Transform.Orientation.z == 3 && inert.Transform.Orientation.w == 4, "SetTransform raw quaternion");
            c.Near(inert.GetLUTValue(int.MinValue), 0, "null LUT minimum");
            c.Near(inert.GetLUTValue(int.MaxValue), 0, "null LUT maximum");
            float negativeZero = BitConverter.ToSingle(BitConverter.GetBytes(unchecked((int)0x80000000)), 0);
            float[] lut = { negativeZero, .25f, .75f };
            Set(inert, "m_tLut", lut);
            c.Is(ReferenceEquals(inert.TLUT, lut), "LUT reference");
            foreach (int index in new[] { int.MinValue, -1, 0, 1, 2, 3, int.MaxValue })
            {
                float expected = index < 0 ? 0 : index >= 3 ? 1 : lut[index];
                c.Is(Bits(inert.GetLUTValue(index)) == Bits(expected), "LUT signed index " + index);
            }
            c.Is(Bits(((ISplineKnotHandle)inert).GetLUTValue(0)) == Bits(negativeZero), "explicit LUT preserves signed zero");
            c.Is(ReferenceEquals(inert.TLUT, lut) && SameBits(lut, new[] { negativeZero, .25f, .75f }), "LUT queries do not change array");
            Set(inert, "m_tLut", new float[0]);
            c.Near(inert.GetLUTValue(-1), 0, "empty LUT negative");
            c.Near(inert.GetLUTValue(0), 1, "empty LUT zero");
            c.Near(inert.GetLUTValue(int.MaxValue), 1, "empty LUT maximum");
            return c.Count;
        }

        public static int EightOriginalKnotThrows()
        {
            Checks c = new Checks();
            InertSplineKnot inert = new InertSplineKnot();
            ISplineKnotRuntimeHandle handle = inert;
            SegmentedSplineKnot segmented = new SegmentedSplineKnot();
            MetadataGroups metadata = new MetadataGroups();
            Set(inert, "m_metadata", metadata); segmented.m_metadata = metadata;
            float[] lut = { .125f, .5f }; Set(inert, "m_tLut", lut);
            c.Throws<NotImplementedException>(() => handle.IsLengthDirty(), "inert IsLengthDirty original throw");
            c.Throws<NotImplementedException>(() => handle.RecalculateLength(), "inert RecalculateLength original throw");
            c.Throws<NotImplementedException>(() => handle.SetLengthDirty(), "inert SetLengthDirty original throw");
            c.Throws<NotImplementedException>(() => { float[] unused = segmented.TLUT; }, "segmented TLUT original throw");
            c.Throws<NotImplementedException>(() => segmented.GetLUTValue(0), "segmented GetLUTValue original throw");
            c.Throws<NotImplementedException>(() => segmented.IsLengthDirty(), "segmented IsLengthDirty original throw");
            c.Throws<NotImplementedException>(() => segmented.RecalculateLength(), "segmented RecalculateLength original throw");
            c.Throws<NotImplementedException>(() => segmented.SetLengthDirty(), "segmented SetLengthDirty original throw");
            c.Is(ReferenceEquals(inert.Metadata, metadata) && ReferenceEquals(segmented.Metadata, metadata), "throw prefix retains metadata");
            c.Is(ReferenceEquals(inert.TLUT, lut) && SameBits(lut, new[] { .125f, .5f }), "throw prefix retains LUT");
            c.Is(Bits(inert.Length) == 0 && Bits(segmented.Length) == 0, "throw prefix retains lengths");
            return c.Count;
        }

        public static int ComponentDefaultsAndParentCache()
        {
            Checks c = new Checks();
            using (OwnedObjects owned = new OwnedObjects())
            {
                EditableSpline editable = owned.Component<EditableSpline>("editable-default");
                InertSpline inert = owned.Component<InertSpline>("inert-default");
                SegmentedSpline segmented = owned.Component<SegmentedSpline>("segmented-default");
                c.Is(editable.SplineType == SplineType.CatmullRom && inert.SplineType == SplineType.CatmullRom
                    && segmented.SplineType == SplineType.CatmullRom, "base original spline type defaults");
                c.Is(editable.KnotCount == 0 && editable.Knots != null && editable.LUTCount == 10, "editable original list/LUT defaults");
                c.Is(inert.KnotCount == 0 && inert.Knots == null && inert.LUTCount == 0, "inert original array/LUT defaults");
                c.Is(segmented.KnotCount == 0 && segmented.Knots == null && segmented.LUTCount == 0, "segmented original array/LUT defaults");
                c.Is(float.IsPositiveInfinity(editable.LengthInverse), "editable unguarded reciprocal");
                c.Is(Bits(inert.LengthInverse) == 0 && Bits(segmented.LengthInverse) == 0, "baked stored reciprocal defaults");
                foreach (ISpline spline in new ISpline[] { editable, inert, segmented })
                {
                    c.Is(spline.GetKnot(int.MinValue) == null, "empty GetKnot minimum");
                    c.Is(spline.GetKnot(int.MaxValue) == null, "empty GetKnot maximum");
                    c.Is(spline.GetHeadKnot() == null, "empty head");
                    c.Is(((ISplineRuntimeHandle)spline).GetKnotRuntimeHandle(-1) == null, "empty runtime knot handle");
                }
                c.Is(editable.GetBackKnot() == null, "empty editable back");
                c.Throws<ArgumentOutOfRangeException>(() => { SplineKnot unused = editable[0]; }, "unguarded empty list indexer");
                c.Throws<NullReferenceException>(() => inert.GetBackKnot(), "null inert back array");
                c.Throws<NullReferenceException>(() => segmented.GetBackKnot(), "null segmented back array");
                c.Throws<NullReferenceException>(() => { InertSplineKnot unused = inert[0]; }, "null inert indexer");
                c.Throws<NullReferenceException>(() => { SegmentedSplineKnot unused = segmented[0]; }, "null segmented indexer");
                Set(inert, "m_knots", new InertSplineKnot[0]); Set(segmented, "m_knots", new SegmentedSplineKnot[0]);
                c.Is(inert.GetBackKnot() == null && segmented.GetBackKnot() == null, "empty baked back arrays");
                c.Throws<NotImplementedException>(() => ((ISplineRuntimeHandle)inert).RecalculateLength(true), "inert original owner throw");
                c.Throws<NotImplementedException>(() => ((ISplineRuntimeHandle)segmented).RecalculateLength(false), "segmented original owner throw");
                SplineKnot knot = owned.Component<SplineKnot>("parent-cached-knot");
                knot.transform.SetParent(editable.transform, false); editable.Knots.Add(knot);
                c.Is(ReferenceEquals(knot.EditableSpline, editable), "first genuine parent lookup");
                c.Is(ReferenceEquals(knot.EditableSpline, editable), "cached same parent");
                c.Is(editable.ContainsKnot(knot), "owned parent membership");
                c.Vector(knot.LocalControlPointNext, Vector3.forward, "original next control default");
                c.Vector(knot.LocalControlPointPrev, -Vector3.forward, "original previous control default");
                c.Is(float.IsPositiveInfinity(knot.LengthInverse) && knot.IsLengthDirty(), "original knot dirty/reciprocal defaults");
                knot.Initialise(3); c.Is(knot.TLUT.Length == 4, "Initialise allocates count plus one");
                knot.Initialise(-1); c.Is(knot.TLUT.Length == 0, "negative one LUT count retained");
                EditableSpline second = owned.Component<EditableSpline>("second-parent");
                knot.transform.SetParent(second.transform, false);
                c.Is(ReferenceEquals(knot.EditableSpline, second), "changed parent invalidates cached owner");
                c.Is(!editable.ContainsKnot(knot) && !second.ContainsKnot(knot), "ownership and list membership remain separate");
                second.Knots.Add(knot); c.Is(second.ContainsKnot(knot), "new owner real membership");
                knot.transform.SetParent(null, false); c.Is(knot.EditableSpline == null, "detached parent clears cache");
            }
            return c.Count;
        }

        private sealed class Line
        {
            public EditableSpline Editable;
            public InertSpline Inert;
            public SegmentedSpline Segmented;
            public SplineKnot First, Last;
        }
        private static Line MakeLine(OwnedObjects owned, Vector3 axis)
        {
            Line line = new Line();
            line.Editable = owned.Component<EditableSpline>("analytic-bezier-line");
            Set(line.Editable, "m_splineType", SplineType.Bezier);
            Set(line.Editable, "m_metadata", new MetadataGroups());
            line.First = owned.Component<SplineKnot>("line-first");
            line.Last = owned.Component<SplineKnot>("line-last");
            line.First.transform.SetParent(line.Editable.transform, false);
            line.Last.transform.SetParent(line.Editable.transform, false);
            line.Last.transform.localPosition = axis * 10f;
            Set(line.First, "m_localControlPointNext", axis * (10f / 3f));
            Set(line.Last, "m_localControlPointPrev", axis * (-10f / 3f));
            Set(line.First, "m_metadata", new MetadataGroups()); Set(line.Last, "m_metadata", new MetadataGroups());
            line.Editable.Knots.Add(line.First); line.Editable.Knots.Add(line.Last);
            line.Editable.Initialise(); line.Editable.RecalculateLength(true);
            InertSplineKnot[] inertKnots = { new InertSplineKnot(), new InertSplineKnot() };
            SegmentedSplineKnot[] segmentedKnots = { new SegmentedSplineKnot(), new SegmentedSplineKnot() };
            for (int i = 0; i < 2; ++i)
            {
                inertKnots[i].Initialise(line.Editable, i);
                segmentedKnots[i].Initialise(line.Editable, i);
            }
            line.Inert = owned.Component<InertSpline>("analytic-inert-line");
            line.Segmented = owned.Component<SegmentedSpline>("analytic-segmented-line");
            foreach (object value in new object[] { line.Inert, line.Segmented })
            {
                Set(value, "m_splineType", SplineType.Bezier);
                Set(value, "m_length", 10f); Set(value, "m_lengthInverse", .1f);
                Set(value, "m_metadata", line.Editable.Metadata);
                Set(value, "<LUTCount>k__BackingField", line.Editable.LUTCount);
                Set(value, "m_transform", new LightweightTransform(Vector3.zero, Quaternion.identity));
            }
            Set(line.Inert, "m_knots", inertKnots); Set(line.Segmented, "m_knots", segmentedKnots);
            return line;
        }

        public static int CopiedLineGeometryAndWorldTransforms()
        {
            Checks c = new Checks();
            using (OwnedObjects owned = new OwnedObjects())
            {
                Line line = MakeLine(owned, Vector3.forward);
                c.Near(line.Editable.Length, 10, "independent straight curve length");
                c.Near(line.First.Length, 10, "first knot straight length");
                c.Near(line.Last.Length, 0, "last knot original zero length");
                c.Is(line.First.TLUT.Length == 10 && line.Last.TLUT.Length == 11, "recalc and terminal LUT allocation differ");
                c.Is(!line.First.IsLengthDirty() && !line.Last.IsLengthDirty(), "recalc clears both dirty flags");
                c.Is(ReferenceEquals(line.Inert[0].Metadata, line.First.Metadata), "inert copied metadata alias");
                c.Is(ReferenceEquals(line.Segmented[0].Metadata, line.First.Metadata), "segmented copied metadata alias");
                c.Is(!ReferenceEquals(line.Inert[0].TLUT, line.First.TLUT) && SameBits(line.Inert[0].TLUT, line.First.TLUT), "inert LUT genuine separate copy");
                float old = line.Inert[0].TLUT[1]; line.First.TLUT[1] = .9375f;
                c.Is(Bits(line.Inert[0].TLUT[1]) == Bits(old), "LUT copy survives source edit"); line.First.TLUT[1] = old;
                c.Is(line.Segmented[0].SegmentSplinePoints != null && line.Segmented[1].SegmentSplinePoints == null, "real segmented terminal null points");
                c.Is(float.IsPositiveInfinity(line.Inert[1].LengthInverse) && Bits(line.Segmented[1].LengthInverse) == 0, "original reciprocal distinction on zero length");
                foreach (float t in new[] { 0f, .25f, .5f, .75f, 1f })
                {
                    Vector3 expected = new Vector3(0, 0, 10 * t);
                    foreach (ISpline spline in new ISpline[] { line.Editable, line.Inert, line.Segmented })
                    {
                        c.Vector(spline.GetLocalPosFromKnotT(new KnotT(0, t)), expected, "analytic line position");
                        PositionAndTangent pair = spline.GetLocalPosAndTangentFromKnotT(new KnotT(0, t));
                        c.Vector(pair.Position, expected, "analytic tangent query retains position");
                        c.Is(pair.Tangent.z > 0 && Math.Abs(pair.Tangent.x) < .002f && Math.Abs(pair.Tangent.y) < .002f, "analytic positive line tangent");
                    }
                }
                foreach (MonoBehaviour component in new MonoBehaviour[] { line.Editable, line.Inert, line.Segmented })
                {
                    component.transform.position = new Vector3(7, -2, 3);
                    component.transform.rotation = Quaternion.Euler(0, 90, 0);
                    component.transform.localScale = new Vector3(2, 3, 4);
                    ISpline spline = (ISpline)component;
                    c.Vector(spline.TransformPoint(new Vector3(0, 0, 2.5f)), new Vector3(17, -2, 3), "explicit scaled world transform");
                    c.Vector(spline.InverseTransformPoint(new Vector3(17, -2, 3)), new Vector3(0, 0, 2.5f), "explicit inverse world transform");
                }
                LinearRatio ratio = line.Segmented.GetLinearRatioFromKnotT(new KnotT(-17, -3.5f));
                c.Is(ratio.KnotIndex == -17 && Bits(ratio.T) == Bits(-3.5f), "segmented ratio repacks without validation");
                KnotT raw = line.Segmented.GetKnotTFromLinearRatio(new LinearRatio(31, 2.5f));
                c.Is(raw.KnotIndex == 31 && Bits(raw.T) == Bits(2.5f), "segmented KnotT repacks without validation");
            }
            return c.Count;
        }

        public static int BoundsCacheAndLocalDistanceQuirk()
        {
            Checks c = new Checks();
            using (OwnedObjects owned = new OwnedObjects())
            {
                Line line = MakeLine(owned, Vector3.right);
                List<ISplineRuntimeHandle.KnotBounds> first = line.Editable.KnotBoundsList;
                c.Is(first.Count == 1 && first[0].KnotIndex == 0, "one curve segment bounds");
                c.Is(ReferenceEquals(first, line.Editable.KnotBoundsList), "same-count bounds cached");
                line.Last.transform.localPosition = new Vector3(20, 0, 0);
                c.Is(ReferenceEquals(first, line.Editable.KnotBoundsList), "same-count moved endpoint does not invalidate bounds");
                line.Last.transform.localPosition = new Vector3(10, 0, 0);
                line.Editable.RecalculateLength(true);
                c.Is(!ReferenceEquals(first, line.Editable.KnotBoundsList), "RecalculateLength invalidates bounds cache");
                foreach (ISpline spline in new ISpline[] { line.Editable, line.Inert, line.Segmented })
                {
                    SurfaceLocation location = spline.FindNearestSurfaceLocationFromLocal(new Vector3(2.5f, 100, 7.5f));
                    c.Is(ReferenceEquals(location.m_surface, spline), "local distance result owns true surface");
                    c.Vector(location.m_localPosition, new Vector3(0, 0, 7.5f), "local z is retained as surface distance", .03f);
                    c.Vector(location.m_worldPosition, new Vector3(7.5f, 0, 0), "world position is the evaluated line point", .03f);
                }
                List<ISplineRuntimeHandle.KnotBounds> inertBounds = line.Inert.KnotBoundsList;
                List<ISplineRuntimeHandle.KnotBounds> segmentedBounds = line.Segmented.KnotBoundsList;
                c.Is(ReferenceEquals(inertBounds, line.Inert.KnotBoundsList), "inert same-count cache");
                c.Is(ReferenceEquals(segmentedBounds, line.Segmented.KnotBoundsList), "segmented same-count cache");
            }
            return c.Count;
        }

        public static int TimestampsAndDelegateFaultPrefixes()
        {
            Checks c = new Checks();
            using (OwnedObjects owned = new OwnedObjects())
            {
                EditableSpline editable = owned.Component<EditableSpline>("inactive-editable");
                InertSpline inert = owned.Component<InertSpline>("inactive-inert");
                SegmentedSpline segmented = owned.Component<SegmentedSpline>("inactive-segmented");
                editable.gameObject.SetActive(false); inert.gameObject.SetActive(false); segmented.gameObject.SetActive(false);
                int editableCalls = 0; editable.OnSplineKnotsUpdated += () => ++editableCalls;
                Set(editable, "m_knots", null);
                editable.KnotsUpdated();
                c.Is(editableCalls == 0 && editable.GetTimestamp() == default(DateTime), "inactive editable returns before null list and timestamp");
                foreach (ISpline spline in new ISpline[] { inert, segmented })
                {
                    List<string> order = new List<string>();
                    bool timestampSeen = false;
                    Action second = () => order.Add("second");
                    Action added = () => order.Add("added");
                    Action first = () =>
                    {
                        timestampSeen = spline.GetTimestamp() != default(DateTime);
                        order.Add("first"); spline.OnSplineKnotsUpdated -= second; spline.OnSplineKnotsUpdated += added;
                    };
                    spline.OnSplineKnotsUpdated += first; spline.OnSplineKnotsUpdated += second;
                    spline.KnotsUpdated();
                    c.Is(timestampSeen, "baked inactive timestamp published before callback");
                    c.Is(order.Count == 2 && order[0] == "first" && order[1] == "second", "captured delegate retains removed callback");
                    order.Clear(); spline.KnotsUpdated();
                    c.Is(order.Count == 2 && order[0] == "first" && order[1] == "added", "next event observes changed invocation list");
                    spline.OnSplineKnotsUpdated -= first; spline.OnSplineKnotsUpdated -= added; spline.OnSplineKnotsUpdated -= added;
                    Exception fault = new InvalidOperationException("owned callback fault");
                    int prefix = 0;
                    spline.OnSplineKnotsUpdated += () => { ++prefix; throw fault; };
                    spline.OnSplineKnotsUpdated += () => ++prefix;
                    Set(spline, "m_timestamp", default(DateTime));
                    c.SameFault(() => spline.KnotsUpdated(), fault, "original callback exception identity");
                    c.Is(prefix == 1, "throw skips later original event callbacks");
                    c.Is(spline.GetTimestamp() != default(DateTime), "timestamp remains stored on callback failure");
                }
                foreach (ISpline spline in new ISpline[] { editable, inert, segmented })
                {
                    ISpline received = null; bool published = false;
                    spline.OnSplineTypeChanged += value => { received = value; published = spline.GetTimestamp() != default(DateTime); };
                    Set(spline, "m_timestamp", default(DateTime)); spline.UpdateSplineType(null);
                    c.Is(received == null && published, "null type argument forwarded after timestamp");
                    spline.UpdateSplineType(inert);
                    c.Is(ReferenceEquals(received, inert), "true spline type argument identity");
                }
            }
            return c.Count;
        }

        public static int FacadePublicationAndOwnedCreation()
        {
            Checks c = new Checks();
            using (OwnedObjects owned = new OwnedObjects())
            {
                Spline facade = owned.Component<Spline>("facade");
                InertSpline inert = owned.Component<InertSpline>("facade-inert");
                SegmentedSpline segmented = owned.Component<SegmentedSpline>("facade-segmented");
                c.Is(facade.GetSpline() == null, "facade initial editable field null");
                facade.SetSpline(null); c.Is(facade.GetSpline() == null, "null replacement early return");
                c.Throws<NullReferenceException>(() => facade.SetSpline(inert), "old null callback faults after replacement");
                c.Is(ReferenceEquals(facade.GetSpline(), inert), "replacement stored before original fault");
                ISpline argument = null; ISpline published = null;
                inert.OnSplineTypeChanged += value => { argument = value; published = facade.GetSpline(); };
                facade.SetSpline(segmented);
                c.Is(ReferenceEquals(argument, segmented) && ReferenceEquals(published, segmented), "old provider receives callback after new selection");
                c.Is(inert.GetTimestamp() != default(DateTime), "old provider timestamp set");
                Exception fault = new InvalidOperationException("owned type callback fault");
                segmented.OnSplineTypeChanged += value => throw fault;
                c.SameFault(() => facade.SetSpline(inert), fault, "replacement callback original exception");
                c.Is(ReferenceEquals(facade.GetSpline(), inert), "new selection survives old provider exception");
                Set(facade, "m_activeSplineType", (SplineVariantType)99);
                c.Throws<ArgumentOutOfRangeException>(() => facade.GetSpline(), "invalid discriminator throws");
                c.Throws<ArgumentOutOfRangeException>(() => facade.SetSpline(segmented), "old invalid discriminator faults before replacement");
                c.Is(Get<SplineVariantType>(facade, "m_activeSplineType") == (SplineVariantType)99, "invalid old selection remains on early fault");
                Spline created = owned.Component<Spline>("owned-creation");
                Set(created, "m_activeSplineType", SplineVariantType.Inert);
                ISpline editable = created.InitialiseSpline();
                c.Is(editable is EditableSpline, "authored child is genuine EditableSpline");
                c.Is(created.GetSpline() == null, "InitialiseSpline preserves discriminator");
                Transform child = ((EditableSpline)editable).transform;
                c.Is(ReferenceEquals(child.parent, created.transform) && child.name == "owned-creation_Spline", "owned child parent and original name");
                c.Vector(child.localPosition, Vector3.zero, "owned child reset position");
                c.Vector(child.localScale, Vector3.one, "owned child reset scale");
                c.Is(child.localRotation == Quaternion.identity, "owned child reset rotation");
                c.Is(ReferenceEquals(created.InitialiseSpline(), editable), "existing genuine interface found without duplicate child");
                c.Is(created.transform.childCount == 1, "owned facade has exactly one created child");
            }
            return c.Count;
        }

        public static int GenuineTrackerWorldAndFaultPrefix()
        {
            Checks c = new Checks();
            using (OwnedObjects owned = new OwnedObjects())
            {
                Line line = MakeLine(owned, Vector3.forward);
                Tracker2D tracker = new Tracker2D(new CollisionResolver2D());
                c.Is(tracker.Location.m_surface == null && tracker.Surfaces == null, "tracker initial references");
                c.Is(!tracker.CheckIfPointIsOnNeighbourBounds(Vector3.zero, Vector3.zero), "empty surface returns before world lookup");
                List<ISurface> surfaces = new List<ISurface> { line.Editable };
                Dictionary<ISurface, ISurface[]> world = new Dictionary<ISurface, ISurface[]> { { line.Editable, new ISurface[0] } };
                tracker.SetWorldInformation(surfaces, world);
                c.Is(ReferenceEquals(tracker.Surfaces, surfaces), "genuine authored surface list retained");
                int callbacks = 0; bool oldNull = false, incomingReal = false, beforeStore = false;
                tracker.OnTrackableSurfaceChange = (oldValue, incoming) =>
                {
                    ++callbacks; oldNull = oldValue.m_surface == null;
                    incomingReal = ReferenceEquals(incoming.m_surface, line.Editable);
                    beforeStore = tracker.Location.m_surface == null;
                };
                tracker.Teleport(new Vector3(2, 0, 5));
                c.Is(callbacks == 1 && oldNull && incomingReal && beforeStore, "true resolver callback before location store");
                c.Is(ReferenceEquals(tracker.Location.m_surface, line.Editable), "true resolver surface published");
                c.Vector(tracker.Location.m_worldPosition, new Vector3(0, 0, 5), "independent nearest straight surface", .04f);
                tracker.Teleport(new Vector3(-3, 0, 6));
                c.Is(callbacks == 1, "same surface does not invoke transition");
                c.Vector(tracker.Location.m_worldPosition, new Vector3(0, 0, 6), "same surface refreshes true position", .04f);
                // The second target is a genuine separately owned curve, never a provider stand-in.
                Line next = MakeLine(owned, Vector3.forward);
                next.Editable.transform.position = new Vector3(20, 0, 0);
                surfaces.Add(next.Editable); world.Add(next.Editable, new ISurface[0]);
                c.Is(tracker.Surfaces.Count == 2, "retained authored list observes additions");
                SurfaceLocation before = tracker.Location;
                Exception fault = new InvalidOperationException("owned tracker callback fault");
                bool sawOld = false, sawNew = false;
                tracker.OnTrackableSurfaceChange = (oldValue, incoming) =>
                {
                    sawOld = ReferenceEquals(oldValue.m_surface, line.Editable);
                    sawNew = ReferenceEquals(incoming.m_surface, next.Editable);
                    throw fault;
                };
                c.SameFault(() => tracker.Teleport(new Vector3(20, 0, 4)), fault, "genuine resolver transition exception");
                c.Is(sawOld && sawNew, "true previous and incoming surface callback arguments");
                c.Is(ReferenceEquals(tracker.Location.m_surface, before.m_surface), "callback failure retains previous surface");
                c.Vector(tracker.Location.m_worldPosition, before.m_worldPosition, "callback failure retains previous complete position");
                tracker.OnTrackableSurfaceChange = null;
                tracker.Teleport(new Vector3(20, 0, 4));
                c.Is(ReferenceEquals(tracker.Location.m_surface, next.Editable), "successful transition after retained failure");
                c.Vector(tracker.Location.m_worldPosition, new Vector3(20, 0, 4), "other true curve world position", .04f);
            }
            return c.Count;
        }

        public static int SharedSamplingCacheFaultRestoration()
        {
            Checks c = new Checks();
            SamplingCache cache = new SamplingCache();
            float[] original = cache.Original; float[] before = cache.Before;
            c.Is(original.Length == 2048 && cache.SameReference, "genuine shared sampling array");
            Exception fault = new InvalidOperationException("owned cleanup fault");
            c.SameFault(() =>
            {
                try
                {
                    original[0] = float.NaN; original[2047] = float.NegativeInfinity;
                    throw fault;
                }
                finally { cache.Dispose(); }
            }, fault, "same fault passes through finally restoration");
            c.Is(cache.SameReference, "cleanup preserves original readonly reference");
            c.Is(SameBits(original, before), "cleanup restores every prior sample bit");
            using (OwnedObjects owned = new OwnedObjects())
            {
                original[0] = float.NaN; original[2047] = float.NegativeInfinity;
                MakeLine(owned, Vector3.forward);
                c.Is(Bits(original[0]) == 0 && !float.IsNegativeInfinity(original[2047]), "genuine length computation overwrites owned sentinel samples");
            }
            c.Is(ReferenceEquals(cache.Original, original) && cache.SameReference, "Engine computation preserves original sampling reference");
            c.Is(SameBits(original, before), "owned Engine cleanup restores every prior sample bit");
            return c.Count;
        }
    }
}
