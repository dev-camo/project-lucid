using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [ExecuteInEditMode]
    public class EditableRibbon : RibbonMonoBase, IRibbon, ISurface, IRibbonRuntimeHandle
    {
        [SerializeField] private DateTime m_timestamp;
        [SerializeField] protected List<RibbonKnot> m_knots = new List<RibbonKnot>();
        [SerializeField] protected float m_leftSplineWidth = 1f;
        [SerializeField] protected float m_rightSplineWidth = 1f;
        [SerializeField] protected RibbonSubSpline m_centerSpline = new RibbonSubSpline(RibbonAlignment.Center);
        [SerializeField] protected RibbonSubSpline m_leftSpline = new RibbonSubSpline(RibbonAlignment.Left);
        [SerializeField] protected RibbonSubSpline m_rightSpline = new RibbonSubSpline(RibbonAlignment.Right);
        [SerializeField] protected int m_lutCount = 10;

        // Original HLSplines.Runtime 0x0600036b..0x06000371; local position precedes local rotation.
        public LightweightTransform Transform => new LightweightTransform(transform.localPosition, transform.localRotation);
        public RibbonSubSpline this[RibbonAlignment alignment] => GetSubSpline(alignment);
        public List<RibbonKnot> Knots => m_knots;
        public int KnotCount => m_knots?.Count ?? 0;
        public int LUTCount => m_lutCount;
        public float Length => m_centerSpline.Length;
        public float LengthInverse => m_centerSpline.LengthInverse;

        // Original 0x06000372..0x06000375; genuine field-like atomic event accessors.
        public event Action OnRibbonKnotsUpdated;
        public event Action<IRibbon> OnRibbonTypeChanged;
        public static int s_lutCountOverride;

        // Original 0x06000376..0x06000378; back count faults before the shared empty/null guard.
        public IRibbonKnot GetHeadKnot(RibbonAlignment alignment) => GetKnot(0, alignment);
        public IRibbonKnot GetBackKnot(RibbonAlignment alignment) => GetKnot(m_knots.Count - 1, alignment);
        public IRibbonKnot GetKnot(int knotIndex, RibbonAlignment alignment)
        {
            if (m_knots == null || m_knots.Count < 1) return null;
            return m_knots[knotIndex][alignment];
        }

        // Original 0x06000379; second owner query and virtual ToString occur before the format/log call.
        public int GetKnotIndex(RibbonSubKnot knot)
        {
            if (knot.Ribbon != this)
            {
                HLOutput.LogError(string.Format("Knot {0} is part of ribbon {1} not us {2}", knot, knot.Ribbon, ToString()));
                return -1;
            }
            return SplineRuntimeComponent.GetKnotIndex(GetSubSpline(knot.Alignment), knot);
        }
        // Original 0x0600037a..0x0600037d.
        public int GetKnotIndex(RibbonKnot knot) => GetKnotIndex(knot[RibbonAlignment.Center]);
        public bool ContainsKnot(RibbonKnot knot)
        {
            if (knot.Ribbon != this) return false;
            return SplineRuntimeComponent.GetKnotIndex(m_centerSpline, knot[RibbonAlignment.Center]) >= 0;
        }
        public RibbonKnot GetKnotBefore(RibbonKnot knot)
        {
            int index = GetKnotIndex(knot);
            return index > 0 ? m_knots[index - 1] : null;
        }
        public RibbonKnot GetKnotAfter(RibbonKnot knot)
        {
            int index = GetKnotIndex(knot) + 1;
            return index < m_knots.Count ? m_knots[index] : null;
        }

        // Original 0x0600037e..0x06000381.
        public MetadataGroups GetSurfaceMetadata() => m_metadata;
        public SurfaceKnotMetadata GetKnotMetadataFromDistance(float distance) => m_centerSpline.GetKnotMetadataFromDistance(distance);
        public SurfaceKnotMetadata GetKnotMetadataFromKnotT(KnotT knotT) => m_centerSpline.GetKnotMetadataFromKnotT(knotT);
        public SurfaceKnotMetadata GetKnotMetadataFromLinearRatio(LinearRatio linearRatio) => m_centerSpline.GetKnotMetadataFromLinearRatio(linearRatio);

        // Original 0x06000382: select spline before fetching the real transform.
        public RibbonLocation FindAdjacentRibbonLocation(Vector3 worldPosition, Vector3 forward, RibbonAlignment alignment)
        {
            RibbonSubSpline subSpline = GetSubSpline(alignment);
            return RibbonRuntimeComponent.FindAdjacentRibbonLocation(this, this, subSpline, transform, worldPosition, forward, alignment);
        }
        // Original 0x06000383: original homogeneous MultiplyPoint, then count/storage, then spline selection.
        public RibbonLocation FindNearestRibbonLocation(Vector3 worldPosition, RibbonAlignment alignment)
        {
            Vector3 localPosition = transform.worldToLocalMatrix.MultiplyPoint(worldPosition);
            InitKnotSortStorage(this, KnotCount, localPosition);
            RibbonSubSpline subSpline = GetSubSpline(alignment);
            return RibbonRuntimeComponent.FindNearestRibbonLocation(this, this, subSpline, m_sortedKnots, localPosition, alignment);
        }
        // Original 0x06000384..0x06000389.
        public RibbonLocation GetRibbonLocationFromDistance(float distance, RibbonAlignment alignment) => RibbonRuntimeComponent.GetRibbonLocationFromDistance(this, GetSubSpline(alignment), distance, alignment);
        public LinearRatio GetLinearRatioFromKnotT(KnotT knotT, RibbonAlignment alignment) => GetSubSpline(alignment).GetLinearRatioFromKnotT(knotT);
        public KnotT GetKnotTFromLinearRatio(LinearRatio linearRatio, RibbonAlignment alignment) => GetSubSpline(alignment).GetKnotTFromLinearRatio(linearRatio);
        public PositionAndTangent GetLocalPosAndTangentFromKnotT(KnotT knotT, RibbonAlignment alignment) => GetSubSpline(alignment).GetLocalPosAndTangentFromKnotT(knotT);
        public LightweightTransform GetLocalTransformFromKnotT(KnotT knotT, RibbonAlignment alignment) => GetSubSpline(alignment).GetLocalTransformFromKnotT(knotT);
        public Vector3 GetLocalPosFromKnotT(KnotT knotT, RibbonAlignment alignment) => GetSubSpline(alignment).GetLocalPosFromKnotT(knotT);
        // Original 0x0600038a..0x0600038c: ratio conversion before the second owner/spline query.
        public PositionAndTangent GetLocalPosAndTangentFromLinearRatio(LinearRatio linearRatio, RibbonAlignment alignment) => GetLocalPosAndTangentFromKnotT(GetKnotTFromLinearRatio(linearRatio, alignment), alignment);
        public LightweightTransform GetLocalTransformFromLinearRatio(LinearRatio linearRatio, RibbonAlignment alignment) => GetLocalTransformFromKnotT(GetKnotTFromLinearRatio(linearRatio, alignment), alignment);
        public Vector3 GetLocalPosFromLinearRatio(LinearRatio linearRatio, RibbonAlignment alignment) => GetLocalPosFromKnotT(GetKnotTFromLinearRatio(linearRatio, alignment), alignment);

        // Original 0x0600038d: side lengths left then right then center, bounds publish last.
        public void RecalculateLength(bool force = false)
        {
            m_leftSpline.RecalculateLength(force);
            m_rightSpline.RecalculateLength(force);
            m_centerSpline.RecalculateLength(force);
            m_boundingBox = RibbonRuntimeComponent.RecalculateBounds(this);
        }
        // Original 0x0600038e: every noncenter enum value uses the side type.
        public SplineType GetSplineType(RibbonAlignment alignment) => alignment == RibbonAlignment.Center ? m_centerSplineType : m_sideSplineType;
        // Original 0x0600038f/0x06000390: genuine invalid-alignment faults.
        public float GetSplineOffset(RibbonAlignment alignment)
        {
            switch (alignment)
            {
                case RibbonAlignment.Left: return -m_leftSplineWidth;
                case RibbonAlignment.Center: return 0f;
                case RibbonAlignment.Right: return m_rightSplineWidth;
                default: throw new ArgumentOutOfRangeException(nameof(alignment), alignment, null);
            }
        }
        public RibbonSubSpline GetSubSpline(RibbonAlignment alignment)
        {
            switch (alignment)
            {
                case RibbonAlignment.Left: return m_leftSpline;
                case RibbonAlignment.Center: return m_centerSpline;
                case RibbonAlignment.Right: return m_rightSpline;
                default: throw new ArgumentOutOfRangeException(nameof(alignment), alignment, null);
            }
        }

        // Original 0x06000391: direct center call; offset applied; point matrix and orientation queried separately.
        public LightweightTransform GetWorldTransformFromDistance(float distance)
        {
            RibbonLocation location = RibbonRuntimeComponent.GetRibbonLocationFromDistance(this, m_centerSpline, distance, RibbonAlignment.Center);
            LightweightTransform local = location.GetLocalTransform(false);
            return new LightweightTransform(transform.localToWorldMatrix.MultiplyPoint3x4(local.Location), transform.rotation * local.Orientation);
        }
        // Original 0x06000392..0x06000396.
        public LightweightTransform GetWorldTransformFromRibbonT(float ribbonT) => GetWorldTransformFromDistance(m_centerSpline.Length * ribbonT);
        public SurfaceLocation FindNearestSurfaceLocationFromWorld(Vector3 worldPosition) => RibbonRuntimeComponent.FindNearestSurfaceLocationFromWorld(this, transform, worldPosition);
        public void KnotsUpdated()
        {
            ClearKnotSortStorage();
            if (gameObject.activeInHierarchy)
            {
                RecalculateLength(true);
                BroadcastKnotsUpdated();
            }
        }
        public void BroadcastKnotsUpdated()
        {
            m_timestamp = DateTime.Now;
            OnRibbonKnotsUpdated?.Invoke();
        }
        public SurfaceLocation FindNearestSurfaceLocationFromLocal(Vector3 localPosition) => RibbonRuntimeComponent.FindNearestSurfaceLocationFromLocal(this, transform, localPosition);
        // Original 0x06000397/0x06000398; false bounds route avoids the Transform call.
        public bool TryGetRaycastHit(Ray ray, float distance, out RaycastHit raycastHit) => RibbonUtilities.TryGetRaycastHit(this, ray, distance, transform, out raycastHit);
        public Bounds GetBoundingBox(bool inWorldSpace = false) => inWorldSpace ? m_boundingBox.LocalToWorld(transform, m_boundsCorners) : m_boundingBox;
        // Original 0x06000399: timestamp store precedes capture/invocation of the original event.
        public void UpdateRibbonType(IRibbon newRibbon)
        {
            m_timestamp = DateTime.Now;
            OnRibbonTypeChanged?.Invoke(newRibbon);
        }
        // Original 0x0600039a..0x0600039f.
        public Vector3 TransformPoint(Vector3 point) => transform.TransformPoint(point);
        public Vector3 InverseTransformPoint(Vector3 point) => transform.InverseTransformPoint(point);
        public bool IsActive() => gameObject.activeInHierarchy;
        public DateTime GetTimestamp() => m_timestamp;
        ISplineKnotRuntimeHandle IRibbonRuntimeHandle.GetKnotRuntimeHandle(int knotIndex, RibbonAlignment alignment) => GetKnot(knotIndex, alignment) as ISplineKnotRuntimeHandle;
        ISplineRuntimeHandle IRibbonRuntimeHandle.GetSubSplineRuntimeHandle(RibbonAlignment alignment) => GetSubSpline(alignment);
        // Original 0x060003a0: publish three old counts, apply override, reload knot list/count every iteration.
        public void Initialise()
        {
            m_centerSpline.Initialise(this);
            m_leftSpline.Initialise(this);
            m_rightSpline.Initialise(this);
            if (s_lutCountOverride > 0) m_lutCount = s_lutCountOverride;
            for (int i = 0; i < m_knots.Count; ++i) m_knots[i].Initialise(m_lutCount);
        }
        // Original 0x060003a1 is the compiler-created constructor for the exact field initializers above.
    }
}
