using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Verification
{
    // Exercises genuine original ribbon objects, callbacks and Unity transforms.
    // Authored state is installed through its original serialized fields; no
    // replacement IRibbon or spline provider is used by these fixtures.
    public static class OriginalRibbonVariantsVerification
    {
        private static int s_checks;
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            ++s_checks;
        }
        private static FieldInfo Field(Type owner, string name)
        {
            for (Type type = owner; type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new MissingFieldException(owner.FullName, name);
        }
        private static object Get(object owner, string name) => Field(owner.GetType(), name).GetValue(owner);
        private static void Set(object owner, string name, object value) => Field(owner.GetType(), name).SetValue(owner, value);
        private static bool Same(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < .000004f;
        private static void Throws<T>(Action action, string message) where T : Exception
        {
            try { action(); }
            catch (T exception) { Check(exception.GetType() == typeof(T), message); return; }
            throw new InvalidOperationException(message + " did not throw");
        }
        private static IRibbon AddRibbon(GameObject owner, bool segmented) => segmented
            ? (IRibbon)owner.AddComponent<SegmentedRibbon>() : owner.AddComponent<InertRibbon>();
        private static object NewSpline(IRibbon owner, bool segmented, RibbonAlignment alignment, float length)
        {
            object spline = segmented ? (object)new SegmentedRibbonSubSpline() : new InertRibbonSubSpline();
            Set(spline, "m_parent", owner);
            Set(spline, "m_alignment", alignment);
            Set(spline, "m_length", length);
            Set(spline, "m_lengthInverse", 1f / length);
            return spline;
        }
        private static Array Splines(bool segmented, params object[] values)
        {
            Array array = Array.CreateInstance(segmented ? typeof(SegmentedRibbonSubSpline) : typeof(InertRibbonSubSpline), values.Length);
            for (int i = 0; i < values.Length; ++i) array.SetValue(values[i], i);
            return array;
        }
        private static object SubSpline(IRibbon owner, RibbonAlignment alignment) => owner is InertRibbon inert
            ? (object)inert.GetSubSpline(alignment) : ((SegmentedRibbon)owner).GetSubSpline(alignment);

        public static int RunCacheAndKnotRules(bool segmented)
        {
            s_checks = 0;
            GameObject gameObject = new GameObject("Project Lucid original ribbon cache verification");
            try
            {
                IRibbon ribbon = AddRibbon(gameObject, segmented);
                Check(ribbon.KnotCount == 0 && ((IRibbonRuntimeHandle)ribbon).LUTCount == 0, "serialized owner defaults");
                Check(ribbon.GetHeadKnot(RibbonAlignment.Center) == null, "head absent array guard");
                Throws<NullReferenceException>(() => ribbon.GetBackKnot(RibbonAlignment.Center), "back reads absent array length first");
                Check(SubSpline(ribbon, RibbonAlignment.Center) == null, "missing spline remains absent");
                object first = NewSpline(ribbon, segmented, RibbonAlignment.Center, 10f);
                object duplicate = NewSpline(ribbon, segmented, RibbonAlignment.Center, 20f);
                object side = NewSpline(ribbon, segmented, RibbonAlignment.Left, 30f);
                Set(ribbon, "m_subSplines", Splines(segmented, first, duplicate, side));
                Check(ReferenceEquals(SubSpline(ribbon, RibbonAlignment.Center), first), "first authored duplicate wins");
                Set(ribbon, "m_subSplines", Splines(segmented, duplicate, side));
                Check(ReferenceEquals(SubSpline(ribbon, RibbonAlignment.Center), first), "captured cache survives array replacement");
                ribbon.KnotsUpdated();
                Check(ReferenceEquals(SubSpline(ribbon, RibbonAlignment.Center), first), "KnotsUpdated retains sub-spline cache");
                Check(ribbon.Length == 10f && ribbon.LengthInverse == .1f, "queries use captured original center entry");
                Check(SubSpline(ribbon, (RibbonAlignment)47) == null, "absent alignment is not manufactured");
                IDictionary cache = (IDictionary)Get(ribbon, "m_cachedSubSplines");
                Check(cache.Count == 1, "missing result is not cached");
                Set(ribbon, "m_subSplines", Splines(segmented, null, side));
                Throws<NullReferenceException>(() => SubSpline(ribbon, RibbonAlignment.Left), "null entry faults before later matching entry");
                Check(cache.Count == 1, "faulting search leaves existing cache intact");
                if (segmented)
                {
                    SegmentedRibbonKnot knot = new SegmentedRibbonKnot();
                    SegmentedRibbonSubKnot subKnot = new SegmentedRibbonSubKnot();
                    Set(knot, "m_subKnots", new[] { subKnot });
                    Set(ribbon, "m_knots", new[] { knot });
                    Check(ribbon.GetKnot(-1, RibbonAlignment.Center) == null, "segmented negative guard");
                    Check(ribbon.GetKnot(1, RibbonAlignment.Center) == null, "segmented upper bound guard");
                    Check(ReferenceEquals(ribbon.GetKnot(0, RibbonAlignment.Center), subKnot), "genuine segmented sub-knot lookup");
                    Check(((IRibbonRuntimeHandle)ribbon).GetKnotRuntimeHandle(-1, RibbonAlignment.Center) == null, "explicit handle retains guarded result");
                }
                else
                {
                    InertRibbonKnot knot = new InertRibbonKnot();
                    InertRibbonSubKnot subKnot = new InertRibbonSubKnot();
                    Set(knot, "m_subKnots", new[] { subKnot });
                    Set(ribbon, "m_knots", new[] { knot });
                    Throws<IndexOutOfRangeException>(() => ribbon.GetKnot(-1, RibbonAlignment.Center), "inert direct negative array index");
                    Throws<IndexOutOfRangeException>(() => ribbon.GetKnot(1, RibbonAlignment.Center), "inert direct upper array index");
                    Check(ReferenceEquals(ribbon.GetKnot(0, RibbonAlignment.Center), subKnot), "genuine inert sub-knot lookup");
                    Throws<IndexOutOfRangeException>(() => ((IRibbonRuntimeHandle)ribbon).GetKnotRuntimeHandle(-1, RibbonAlignment.Center), "explicit inert handle retains index fault");
                }
                Check((segmented ? ((SegmentedRibbon)ribbon).GetSplineType((RibbonAlignment)47) : ((InertRibbon)ribbon).GetSplineType((RibbonAlignment)47)) == SplineType.CatmullRom, "noncenter enum uses original side type");
                return s_checks;
            }
            finally { UnityEngine.Object.DestroyImmediate(gameObject); }
        }

        public static int RunTransformsAndBounds(bool segmented)
        {
            s_checks = 0;
            GameObject gameObject = new GameObject("Project Lucid original ribbon transform verification");
            try
            {
                IRibbon ribbon = AddRibbon(gameObject, segmented);
                LightweightTransform stored = new LightweightTransform(new Vector3(101f, 102f, 103f), Quaternion.Euler(10f, 20f, 30f));
                Set(ribbon, "m_transform", stored);
                gameObject.transform.position = new Vector3(4f, 5f, 6f);
                gameObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                gameObject.transform.localScale = new Vector3(2f, 3f, 4f);
                Check(Same(ribbon.Transform.Location, stored.Location), "Transform reads serialized snapshot");
                Check(Quaternion.Angle(ribbon.Transform.Orientation, stored.Orientation) < .001f, "snapshot orientation retained");
                Vector3 local = new Vector3(1f, 2f, 3f);
                Vector3 world = new Vector3(16f, 11f, 4f);
                Check(Same(ribbon.TransformPoint(local), world), "actual Unity transform with scale and rotation");
                Check(Same(ribbon.InverseTransformPoint(world), local), "actual inverse matrix");
                Bounds bounds = new Bounds(new Vector3(1f, 2f, 3f), new Vector3(2f, 4f, 6f));
                Set(ribbon, "m_boundingBox", bounds);
                Check(ribbon.GetBoundingBox().Equals(bounds), "default is stored local bounds");
                Bounds worldBounds = ribbon.GetBoundingBox(true);
                Check(Same(worldBounds.center, world), "world bounds use actual transform center");
                Check(Same(worldBounds.size, new Vector3(24f, 12f, 4f)), "all actual transformed corners contribute");
                Check(ribbon.IsActive(), "actual active hierarchy");
                gameObject.SetActive(false);
                Check(!ribbon.IsActive(), "inactive component still reports hierarchy state");
                Check(Same(ribbon.Transform.Location, stored.Location), "Unity movement does not overwrite snapshot");
                Check(((Vector3[])Get(ribbon, "m_boundsCorners")).Length == 8, "original reusable corner storage");
                return s_checks;
            }
            finally { UnityEngine.Object.DestroyImmediate(gameObject); }
        }

        public static int RunEventOrdering(bool segmented)
        {
            s_checks = 0;
            GameObject gameObject = new GameObject("Project Lucid original ribbon event verification");
            try
            {
                IRibbon ribbon = AddRibbon(gameObject, segmented);
                object cached = NewSpline(ribbon, segmented, RibbonAlignment.Center, 12f);
                Set(ribbon, "m_subSplines", Splines(segmented, cached));
                SubSpline(ribbon, RibbonAlignment.Center);
                Set(ribbon, "m_knotBounds", new List<Bounds>());
                Set(ribbon, "m_sortedKnots", new List<SortKnots>());
                Set(ribbon, "m_timestamp", new DateTime(2000, 1, 1));
                int calls = 0;
                Action second = () => ++calls;
                Action first = () =>
                {
                    Check(Get(ribbon, "m_knotBounds") == null && Get(ribbon, "m_sortedKnots") == null, "sorting storage cleared before callback");
                    Check(ribbon.GetTimestamp().Year > 2000 && ribbon.GetTimestamp().Kind == DateTimeKind.Local, "local timestamp published before callback");
                    Check(ReferenceEquals(SubSpline(ribbon, RibbonAlignment.Center), cached), "callback sees preserved cache");
                    ++calls;
                    ribbon.OnRibbonKnotsUpdated -= second;
                };
                ribbon.OnRibbonKnotsUpdated += first;
                ribbon.OnRibbonKnotsUpdated += second;
                ribbon.KnotsUpdated();
                Check(calls == 2, "captured multicast retains removed listener for current invocation");
                ribbon.KnotsUpdated();
                Check(calls == 3, "next invocation observes removal");
                ribbon.OnRibbonKnotsUpdated -= first;
                Action fail = () => throw new InvalidOperationException("expected callback fault");
                ribbon.OnRibbonKnotsUpdated += fail;
                Set(ribbon, "m_timestamp", new DateTime(2000, 1, 1));
                Throws<InvalidOperationException>(() => ribbon.KnotsUpdated(), "callback failure propagates");
                Check(ribbon.GetTimestamp().Year > 2000, "callback fault retains published timestamp");
                ribbon.OnRibbonKnotsUpdated -= fail;
                IRibbon received = null;
                ribbon.OnRibbonTypeChanged += value =>
                {
                    Check(ribbon.GetTimestamp().Year > 2000, "type event observes timestamp store");
                    received = value;
                };
                Set(ribbon, "m_timestamp", new DateTime(2000, 1, 1));
                ribbon.UpdateRibbonType(ribbon);
                Check(ReferenceEquals(received, ribbon), "type event receives exact original interface");
                gameObject.SetActive(false);
                Set(ribbon, "m_timestamp", new DateTime(2000, 1, 1));
                ribbon.KnotsUpdated();
                Check(ribbon.GetTimestamp().Year > 2000, "variant notification has no inactive-owner gate");
                Check(ribbon.Length == 12f, "notification does not recalculate stored length");
                return s_checks;
            }
            finally { UnityEngine.Object.DestroyImmediate(gameObject); }
        }

        public static int RunOriginalGeometry(bool segmented)
        {
            s_checks = 0;
            GameObject authoredObject = null;
            GameObject recoveredObject = null;
            int oldOverride = EditableRibbon.s_lutCountOverride;
            Type runtime = typeof(KnotT).Assembly.GetType("Hardlight.SplineRuntimeComponent", true);
            float[] sharedLut = (float[])runtime.GetField("s_lutCache", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            float[] oldLut = (float[])sharedLut.Clone();
            try
            {
                EditableRibbon.s_lutCountOverride = 0;
                authoredObject = new GameObject("Project Lucid original authored geometry verification");
                EditableRibbon authored = authoredObject.AddComponent<EditableRibbon>();
                GameObject firstObject = new GameObject("first authored knot");
                firstObject.transform.SetParent(authoredObject.transform, false);
                RibbonKnot first = firstObject.AddComponent<RibbonKnot>();
                GameObject lastObject = new GameObject("last authored knot");
                lastObject.transform.SetParent(authoredObject.transform, false);
                lastObject.transform.localPosition = new Vector3(0f, 0f, 10f);
                RibbonKnot last = lastObject.AddComponent<RibbonKnot>();
                authored.Knots.Add(first);
                authored.Knots.Add(last);
                authored.Initialise();
                Set(authored, "m_centerSplineType", SplineType.Bezier);
                Set(authored, "m_sideSplineType", SplineType.Bezier);
                first[RibbonAlignment.Center].LocalControlPointNext = Vector3.forward * (10f / 3f);
                last[RibbonAlignment.Center].LocalControlPointPrev = -Vector3.forward * (10f / 3f);
                authored.RecalculateLength(true);

                recoveredObject = new GameObject("Project Lucid original captured geometry verification");
                IRibbon ribbon = AddRibbon(recoveredObject, segmented);
                Set(ribbon, "m_transform", authored.Transform);
                Set(ribbon, "m_centerSplineType", SplineType.Bezier);
                Set(ribbon, "m_sideSplineType", SplineType.Bezier);
                Set(ribbon, "m_boundingBox", authored.GetBoundingBox(false));
                if (segmented)
                {
                    SegmentedRibbonKnot[] knots = { new SegmentedRibbonKnot(), new SegmentedRibbonKnot() };
                    knots[0].Initialise(authored, 0, Matrix4x4.identity);
                    knots[1].Initialise(authored, 1, Matrix4x4.identity);
                    Set(ribbon, "m_knots", knots);
                    SegmentedRibbonSubSpline[] splines = { new SegmentedRibbonSubSpline(), new SegmentedRibbonSubSpline(), new SegmentedRibbonSubSpline() };
                    splines[0].Initialise((SegmentedRibbon)ribbon, authored.GetSubSpline(RibbonAlignment.Center));
                    splines[1].Initialise((SegmentedRibbon)ribbon, authored.GetSubSpline(RibbonAlignment.Right));
                    splines[2].Initialise((SegmentedRibbon)ribbon, authored.GetSubSpline(RibbonAlignment.Left));
                    Set(ribbon, "m_subSplines", splines);
                }
                else
                {
                    InertRibbonKnot[] knots = { new InertRibbonKnot(), new InertRibbonKnot() };
                    knots[0].Initialise(authored, 0);
                    knots[1].Initialise(authored, 1);
                    Set(ribbon, "m_knots", knots);
                    InertRibbonSubSpline[] splines = { new InertRibbonSubSpline(), new InertRibbonSubSpline(), new InertRibbonSubSpline() };
                    splines[0].Initialise((InertRibbon)ribbon, authored.GetSubSpline(RibbonAlignment.Center));
                    splines[1].Initialise((InertRibbon)ribbon, authored.GetSubSpline(RibbonAlignment.Right));
                    splines[2].Initialise((InertRibbon)ribbon, authored.GetSubSpline(RibbonAlignment.Left));
                    Set(ribbon, "m_subSplines", splines);
                }
                Check(ribbon.KnotCount == 2, "two genuine captured knot owners");
                Check(Math.Abs(ribbon.Length - 10f) < .002f && Math.Abs(ribbon.LengthInverse - .1f) < .00002f, "original stored/subsampled straight length");
                Check(Same(ribbon.GetLocalPosFromKnotT(new KnotT(0, .5f), RibbonAlignment.Center), Vector3.forward * 5f), "actual original center curve midpoint");
                Check(Same(ribbon.GetLocalPosFromKnotT(new KnotT(0, .5f), RibbonAlignment.Left), new Vector3(-1f, 0f, 5f)), "actual left captured curve");
                Check(Same(ribbon.GetLocalPosFromKnotT(new KnotT(0, .5f), RibbonAlignment.Right), new Vector3(1f, 0f, 5f)), "actual right captured curve");
                PositionAndTangent point = ribbon.GetLocalPosAndTangentFromKnotT(new KnotT(0, .5f), RibbonAlignment.Center);
                Check(Same(point.Position, Vector3.forward * 5f), "genuine tangent query position");
                Check(Same(point.Tangent.normalized, Vector3.forward), "genuine straight tangent direction");
                float expectedT = segmented ? .5f : .49951171875f;
                KnotT knotT = ribbon.GetKnotTFromLinearRatio(new LinearRatio(0, .5f), RibbonAlignment.Center);
                Check(knotT.KnotIndex == 0 && knotT.T == expectedT, "inert LUT quantization and segmented identity conversion remain distinct");
                LinearRatio ratio = ribbon.GetLinearRatioFromKnotT(knotT, RibbonAlignment.Center);
                Check(ratio.KnotIndex == 0 && ratio.T == .5f, "original captured conversion round trip");
                Check(Same(ribbon.GetLocalPosFromLinearRatio(new LinearRatio(0, .5f), RibbonAlignment.Center), Vector3.forward * (10f * expectedT)), "original linear query retains expected numeric path");
                Check(Quaternion.Angle(ribbon.GetLocalTransformFromKnotT(new KnotT(0, .5f), RibbonAlignment.Center).Orientation, Quaternion.identity) < .001f, "genuine curve orientation");
                ISplineRuntimeHandle spline = ((IRibbonRuntimeHandle)ribbon).GetSubSplineRuntimeHandle(RibbonAlignment.Center);
                Check(spline.LUTCount == 10 && Math.Abs(spline.Length - 10f) < .002f, "authored source count captured by original sub-spline initialization");
                Check(Same(ribbon.GetBackKnot(RibbonAlignment.Center).WorldControlPointPrev, -Vector3.forward * (10f / 3f)), "shipping world-named control copy retains local vector");
                Check(ReferenceEquals(((IRibbonRuntimeHandle)ribbon).GetKnotRuntimeHandle(1, RibbonAlignment.Center), ribbon.GetBackKnot(RibbonAlignment.Center)), "actual explicit handle resolves captured instance");
                RaycastHit hit;
                Check(ribbon.TryGetRaycastHit(new Ray(new Vector3(.25f, 3f, 5f), Vector3.down), 4f, out hit), "original triangulated surface accepts front ray");
                Check(Same(hit.point, new Vector3(.25f, 0f, 5f)) && Math.Abs(hit.distance - 3f) < .002f, "actual ray point and distance");
                Check(!ribbon.TryGetRaycastHit(new Ray(new Vector3(.25f, 3f, 5f), Vector3.down), 3f, out hit) && hit.distance == 4f, "native distance tie rejected with initialized miss");
                recoveredObject.transform.position = new Vector3(3f, 4f, 5f);
                recoveredObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                recoveredObject.transform.localScale = new Vector3(2f, 1f, 2f);
                Vector3 expectedWorld = new Vector3(3f + 20f * expectedT, 4f, 5f);
                Check(Same(ribbon.GetWorldTransformFromDistance(5f).Location, expectedWorld) && Same(ribbon.GetWorldTransformFromRibbonT(.5f).Location, expectedWorld), "world location uses actual matrix after original distance conversion");
                Check(Quaternion.Angle(ribbon.GetWorldTransformFromDistance(5f).Orientation, recoveredObject.transform.rotation) < .001f, "world orientation uses actual Unity rotation");
                Check(Same(ribbon.Transform.Location, Vector3.zero) && Same(ribbon.GetLocalPosFromKnotT(new KnotT(0, .5f), RibbonAlignment.Center), Vector3.forward * 5f), "moving component retains captured local geometry and transform snapshot");
                return s_checks;
            }
            finally
            {
                EditableRibbon.s_lutCountOverride = oldOverride;
                try
                {
                    if (recoveredObject != null) UnityEngine.Object.DestroyImmediate(recoveredObject);
                }
                finally
                {
                    try { if (authoredObject != null) UnityEngine.Object.DestroyImmediate(authoredObject); }
                    finally { Array.Copy(oldLut, sharedLut, oldLut.Length); }
                }
            }
        }

        public static int RunFacadeSelection()
        {
            s_checks = 0;
            GameObject owner = new GameObject("Project Lucid original ribbon selector verification");
            try
            {
                Ribbon facade = owner.AddComponent<Ribbon>();
                EditableRibbon editable = owner.AddComponent<EditableRibbon>();
                InertRibbon inert = owner.AddComponent<InertRibbon>();
                SegmentedRibbon segmented = owner.AddComponent<SegmentedRibbon>();
                Set(facade, "m_editableRibbon", editable);
                Set(facade, "m_inertRibbon", inert);
                Set(facade, "m_segmentedRibbon", segmented);
                Set(facade, "m_activeRibbonType", RibbonVariantType.Editable);
                Check(ReferenceEquals(facade.GetRibbon(), editable), "original editable enum identity");
                Set(facade, "m_activeRibbonType", RibbonVariantType.Inert);
                Check(ReferenceEquals(facade.GetRibbon(), inert), "original inert enum identity");
                Set(facade, "m_activeRibbonType", RibbonVariantType.Segmented);
                Check(ReferenceEquals(facade.GetRibbon(), segmented), "original segmented enum identity");
                Set(facade, "m_activeRibbonType", (RibbonVariantType)47);
                Throws<ArgumentOutOfRangeException>(() => facade.GetRibbon(), "invalid enum does not choose available reference");
                facade.SetRibbon(null);
                Check((RibbonVariantType)Get(facade, "m_activeRibbonType") == (RibbonVariantType)47, "null setter returns before invalid old selection");
                return s_checks;
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }

        public static int RunFacadeCreation()
        {
            s_checks = 0;
            int oldOverride = EditableRibbon.s_lutCountOverride;
            GameObject owner = new GameObject("Project Lucid original ribbon creation verification");
            try
            {
                Ribbon facade = owner.AddComponent<Ribbon>();
                owner.transform.position = new Vector3(4f, 5f, 6f);
                owner.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
                owner.transform.localScale = new Vector3(2f, 3f, 4f);
                EditableRibbon created = facade.InitialiseRibbon(17) as EditableRibbon;
                Check(created != null && ReferenceEquals(Get(facade, "m_editableRibbon"), created), "original component and serialized reference published");
                Check(created.transform.parent == owner.transform, "new ribbon is an actual child");
                Check(created.name == owner.name + "_Ribbon", "shipped child suffix");
                Check(Same(created.transform.localPosition, Vector3.zero), "child position reset");
                Check(Quaternion.Angle(created.transform.localRotation, Quaternion.identity) < .001f, "child rotation reset");
                Check(Same(created.transform.localScale, Vector3.one), "child scale reset");
                Check(created.LUTCount == 17 && EditableRibbon.s_lutCountOverride == 0, "override reaches original initialization and resets after success");
                Check(created.GetSubSpline(RibbonAlignment.Center).LUTCount == 10, "sub-spline captures count before owner override");
                Set(facade, "m_editableRibbon", null);
                Set(facade, "m_activeRibbonType", (RibbonVariantType)47);
                EditableRibbon.s_lutCountOverride = 29;
                Check(ReferenceEquals(facade.InitialiseRibbon(33), created), "existing original child returned without reinitializing");
                Check(Get(facade, "m_editableRibbon") == null, "existing-child branch does not assign field");
                Check((RibbonVariantType)Get(facade, "m_activeRibbonType") == (RibbonVariantType)47, "existing-child branch does not change enum");
                Check(created.LUTCount == 17 && EditableRibbon.s_lutCountOverride == 29, "early return preserves original stored count and static override");
                return s_checks;
            }
            finally
            {
                EditableRibbon.s_lutCountOverride = oldOverride;
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        public static int RunFacadePublication()
        {
            s_checks = 0;
            GameObject owner = new GameObject("Project Lucid original ribbon publication verification");
            try
            {
                Ribbon facade = owner.AddComponent<Ribbon>();
                InertRibbon oldRibbon = owner.AddComponent<InertRibbon>();
                SegmentedRibbon next = owner.AddComponent<SegmentedRibbon>();
                Set(facade, "m_inertRibbon", oldRibbon);
                Set(facade, "m_activeRibbonType", RibbonVariantType.Inert);
                int callbacks = 0;
                oldRibbon.OnRibbonTypeChanged += value =>
                {
                    Check(ReferenceEquals(value, next), "old instance receives exact new interface");
                    Check(ReferenceEquals(facade.GetRibbon(), next), "new enum/reference published before callback");
                    Check(oldRibbon.GetTimestamp().Year > 2000, "old ribbon timestamp precedes callback");
                    ++callbacks;
                };
                facade.SetRibbon(next);
                Check(callbacks == 1, "single original old-ribbon notification");
                Check(ReferenceEquals(Get(facade, "m_inertRibbon"), oldRibbon), "old authored reference remains stored");
                Set(facade, "m_inertRibbon", null);
                Set(facade, "m_activeRibbonType", RibbonVariantType.Inert);
                Throws<NullReferenceException>(() => facade.SetRibbon(next), "missing old callback receiver faults after publication");
                Check(ReferenceEquals(facade.GetRibbon(), next), "callback fault retains published new selection");
                return s_checks;
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }
    }
}
