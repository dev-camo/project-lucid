using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [ExecuteInEditMode]
    public class InertRibbon : RibbonMonoBase, IRibbon, ISurface, IRibbonRuntimeHandle
    {
        [SerializeField] private DateTime m_timestamp;
        [SerializeField] private LightweightTransform m_transform;
        [SerializeField] private InertRibbonKnot[] m_knots;
        [SerializeField] private InertRibbonSubSpline[] m_subSplines;
        private readonly Dictionary<RibbonAlignment, InertRibbonSubSpline> m_cachedSubSplines =
            new Dictionary<RibbonAlignment, InertRibbonSubSpline>(HLSplinesEnumComparers.RibbonAlignmentComparer);

        // Original HLSplines.Runtime 0x060003e3..0x060003eb. Transform is the
        // serialized value; actual surface queries below use the Unity transform.
        public LightweightTransform Transform => m_transform;
        public InertRibbonSubSpline this[RibbonAlignment alignment] => GetSubSpline(alignment);
        public InertRibbonKnot[] Knots => m_knots;
        public InertRibbonKnot this[int knotIndex] => m_knots[knotIndex];
        public int KnotCount => m_knots?.Length ?? 0;
        public float Length => GetSubSpline(RibbonAlignment.Center).Length;
        public float LengthInverse => GetSubSpline(RibbonAlignment.Center).LengthInverse;
        public int LUTCount { get; private set; }

        // Original 0x060003ec..0x060003ef: field-like atomic event accessors.
        public event Action OnRibbonKnotsUpdated;
        public event Action<IRibbon> OnRibbonTypeChanged;

        // Original 0x060003f0..0x060003f2. Back evaluates the array length
        // before GetKnot's null/empty guard; invalid nonempty indices still fault.
        public IRibbonKnot GetHeadKnot(RibbonAlignment alignment) => GetKnot(0, alignment);
        public IRibbonKnot GetBackKnot(RibbonAlignment alignment) => GetKnot(m_knots.Length - 1, alignment);
        public IRibbonKnot GetKnot(int knotIndex, RibbonAlignment alignment)
        {
            if (m_knots == null || m_knots.Length < 1) return null;
            return m_knots[knotIndex].GetSubKnot(alignment);
        }

        // Original 0x060003f3..0x060003f5. Unlike EditableRibbon, the shipped
        // inert queries do not first compare the knot's ribbon owner to this.
        public int GetKnotIndex(InertRibbonSubKnot knot) => GetSubSpline(knot.Alignment).GetKnotIndex(knot);
        public int GetKnotIndex(InertRibbonKnot knot) => GetKnotIndex(knot.GetSubKnot(RibbonAlignment.Center));
        public bool ContainsKnot(InertRibbonKnot knot)
        {
            InertRibbonSubSpline subSpline = GetSubSpline(RibbonAlignment.Center);
            return subSpline.GetKnotIndex(knot.GetSubKnot(RibbonAlignment.Center)) >= 0;
        }

        // Original 0x060003f6/0x060003f7. A missing knot has index -1, so the
        // original after-query returns the first knot of a nonempty array.
        public InertRibbonKnot GetKnotBefore(InertRibbonKnot knot)
        {
            int index = GetKnotIndex(knot);
            return index > 0 ? m_knots[index - 1] : null;
        }
        public InertRibbonKnot GetKnotAfter(InertRibbonKnot knot)
        {
            int index = GetKnotIndex(knot) + 1;
            return index < m_knots.Length ? m_knots[index] : null;
        }

        // Original 0x060003f8..0x060003fb: select the current center spline
        // separately for each metadata query; preserve its original lookup faults.
        public MetadataGroups GetSurfaceMetadata() => m_metadata;
        public SurfaceKnotMetadata GetKnotMetadataFromDistance(float distance) => GetSubSpline(RibbonAlignment.Center).GetKnotMetadataFromDistance(distance);
        public SurfaceKnotMetadata GetKnotMetadataFromKnotT(KnotT knotT) => GetSubSpline(RibbonAlignment.Center).GetKnotMetadataFromKnotT(knotT);
        public SurfaceKnotMetadata GetKnotMetadataFromLinearRatio(LinearRatio linearRatio) => GetSubSpline(RibbonAlignment.Center).GetKnotMetadataFromLinearRatio(linearRatio);

        // Original 0x060003fc: spline selection precedes the transform lookup.
        public RibbonLocation FindAdjacentRibbonLocation(Vector3 worldPosition, Vector3 forward, RibbonAlignment alignment)
        {
            InertRibbonSubSpline subSpline = GetSubSpline(alignment);
            return RibbonRuntimeComponent.FindAdjacentRibbonLocation(this, this, subSpline, transform, worldPosition, forward, alignment);
        }
        // Original 0x060003fd: homogeneous MultiplyPoint, then count/storage,
        // then spline selection. The serialized LightweightTransform is unused.
        public RibbonLocation FindNearestRibbonLocation(Vector3 worldPosition, RibbonAlignment alignment)
        {
            Vector3 localPosition = transform.worldToLocalMatrix.MultiplyPoint(worldPosition);
            InitKnotSortStorage(this, KnotCount, localPosition);
            InertRibbonSubSpline subSpline = GetSubSpline(alignment);
            return RibbonRuntimeComponent.FindNearestRibbonLocation(this, this, subSpline, m_sortedKnots, localPosition, alignment);
        }

        // Original 0x060003fe..0x06000403; original concrete spline methods
        // retain the LUT conversion and full position/rotation calculations.
        public RibbonLocation GetRibbonLocationFromDistance(float distance, RibbonAlignment alignment) => RibbonRuntimeComponent.GetRibbonLocationFromDistance(this, GetSubSpline(alignment), distance, alignment);
        public LinearRatio GetLinearRatioFromKnotT(KnotT knotT, RibbonAlignment alignment) => GetSubSpline(alignment).GetLinearRatioFromKnotT(knotT);
        public KnotT GetKnotTFromLinearRatio(LinearRatio linearRatio, RibbonAlignment alignment) => GetSubSpline(alignment).GetKnotTFromLinearRatio(linearRatio);
        public PositionAndTangent GetLocalPosAndTangentFromKnotT(KnotT knotT, RibbonAlignment alignment) => GetSubSpline(alignment).GetLocalPosAndTangentFromKnotT(knotT);
        public LightweightTransform GetLocalTransformFromKnotT(KnotT knotT, RibbonAlignment alignment) => GetSubSpline(alignment).GetLocalTransformFromKnotT(knotT);
        public Vector3 GetLocalPosFromKnotT(KnotT knotT, RibbonAlignment alignment) => GetSubSpline(alignment).GetLocalPosFromKnotT(knotT);

        // Original 0x06000404..0x06000406: conversion before the second query;
        // do not cache one spline across both original calls.
        public PositionAndTangent GetLocalPosAndTangentFromLinearRatio(LinearRatio linearRatio, RibbonAlignment alignment) => GetLocalPosAndTangentFromKnotT(GetKnotTFromLinearRatio(linearRatio, alignment), alignment);
        public LightweightTransform GetLocalTransformFromLinearRatio(LinearRatio linearRatio, RibbonAlignment alignment) => GetLocalTransformFromKnotT(GetKnotTFromLinearRatio(linearRatio, alignment), alignment);
        public Vector3 GetLocalPosFromLinearRatio(LinearRatio linearRatio, RibbonAlignment alignment) => GetLocalPosFromKnotT(GetKnotTFromLinearRatio(linearRatio, alignment), alignment);

        // Original 0x06000407: every noncenter alignment selects the side type.
        public SplineType GetSplineType(RibbonAlignment alignment) => alignment == RibbonAlignment.Center ? m_centerSplineType : m_sideSplineType;

        // Original 0x06000408. First matching array entry is added to the cache;
        // duplicate-key faults, null entries, and the original missing result remain.
        public InertRibbonSubSpline GetSubSpline(RibbonAlignment alignment)
        {
            if (!m_cachedSubSplines.TryGetValue(alignment, out InertRibbonSubSpline subSpline))
            {
                if (m_subSplines != null && m_subSplines.Length > 0)
                {
                    foreach (InertRibbonSubSpline candidate in m_subSplines)
                    {
                        if (candidate.Alignment == alignment)
                        {
                            m_cachedSubSplines.Add(alignment, candidate);
                            subSpline = candidate;
                            break;
                        }
                    }
                }
            }
            return subSpline;
        }

        // Original 0x06000409. The location's offset is applied before separate
        // real transform queries for the point matrix and world orientation.
        public LightweightTransform GetWorldTransformFromDistance(float distance)
        {
            RibbonLocation location = RibbonRuntimeComponent.GetRibbonLocationFromDistance(this, GetSubSpline(RibbonAlignment.Center), distance, RibbonAlignment.Center);
            LightweightTransform local = location.GetLocalTransform(false);
            return new LightweightTransform(transform.localToWorldMatrix.MultiplyPoint3x4(local.Location), transform.rotation * local.Orientation);
        }
        // Original 0x0600040a..0x0600040e. Local bounds avoid transform lookup.
        public LightweightTransform GetWorldTransformFromRibbonT(float ribbonT) => GetWorldTransformFromDistance(Length * ribbonT);
        public SurfaceLocation FindNearestSurfaceLocationFromWorld(Vector3 worldPosition) => RibbonRuntimeComponent.FindNearestSurfaceLocationFromWorld(this, transform, worldPosition);
        public SurfaceLocation FindNearestSurfaceLocationFromLocal(Vector3 localPosition) => RibbonRuntimeComponent.FindNearestSurfaceLocationFromLocal(this, transform, localPosition);
        public bool TryGetRaycastHit(Ray ray, float distance, out RaycastHit raycastHit) => RibbonUtilities.TryGetRaycastHit(this, ray, distance, transform, out raycastHit);
        public Bounds GetBoundingBox(bool inWorldSpace = false) => inWorldSpace ? m_boundingBox.LocalToWorld(transform, m_boundsCorners) : m_boundingBox;

        // Original 0x0600040f clears sorting storage before timestamp publication
        // and notification. It neither recalculates length nor clears spline cache.
        public void KnotsUpdated()
        {
            ClearKnotSortStorage();
            m_timestamp = DateTime.Now;
            OnRibbonKnotsUpdated?.Invoke();
        }
        // Original 0x06000410: publish time before capturing the change event.
        public void UpdateRibbonType(IRibbon newRibbon)
        {
            m_timestamp = DateTime.Now;
            OnRibbonTypeChanged?.Invoke(newRibbon);
        }

        // Original 0x06000411..0x06000414 use the actual component hierarchy.
        public Vector3 TransformPoint(Vector3 point) => transform.TransformPoint(point);
        public Vector3 InverseTransformPoint(Vector3 point) => transform.InverseTransformPoint(point);
        public bool IsActive() => gameObject.activeInHierarchy;
        public DateTime GetTimestamp() => m_timestamp;

        // Original 0x06000415..0x06000419 preserve the separate explicit
        // interface methods and the original safe interface test for a knot.
        LinearRatio IRibbonRuntimeHandle.GetLinearRatioFromKnotT(KnotT knotT, RibbonAlignment alignment) => GetLinearRatioFromKnotT(knotT, alignment);
        KnotT IRibbonRuntimeHandle.GetKnotTFromLinearRatio(LinearRatio linearRatio, RibbonAlignment alignment) => GetKnotTFromLinearRatio(linearRatio, alignment);
        PositionAndTangent IRibbonRuntimeHandle.GetLocalPosAndTangentFromKnotT(KnotT knotT, RibbonAlignment alignment) => GetLocalPosAndTangentFromKnotT(knotT, alignment);
        ISplineKnotRuntimeHandle IRibbonRuntimeHandle.GetKnotRuntimeHandle(int knotIndex, RibbonAlignment alignment) => GetKnot(knotIndex, alignment) as ISplineKnotRuntimeHandle;
        ISplineRuntimeHandle IRibbonRuntimeHandle.GetSubSplineRuntimeHandle(RibbonAlignment alignment) => GetSubSpline(alignment);

        // Original 0x0600041a is private; its cached dictionary initializer runs
        // before the original RibbonMonoBase constructor and its bounds storage.
        private InertRibbon() { }
    }
}
