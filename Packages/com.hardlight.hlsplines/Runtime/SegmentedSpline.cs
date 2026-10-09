using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLSplines.Runtime 0x020000a8, complete 59-method owner including its callback.
    // Bodies inferred from complete ARM64/x86 captures; authored text and native fault scheduling remain qualified.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [ExecuteInEditMode]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Serializable]
    public class SegmentedSpline : SplineMonoBase, ISpline, ISurface, ISegmentedSplineRuntimeHandle, ISplineRuntimeHandle
    {
        [SerializeField] private DateTime m_timestamp;
        [SerializeField] private LightweightTransform m_transform;
        [SerializeField] private SegmentedSplineKnot[] m_knots;
        [SerializeField] private float m_lengthInverse;
        private List<ISplineRuntimeHandle.KnotBounds> m_knotBoundsList;

        public LightweightTransform Transform => m_transform;
        public SegmentedSplineKnot[] Knots => m_knots;
        public SegmentedSplineKnot this[int knotIndex] => m_knots[knotIndex];
        public int KnotCount => m_knots == null ? 0 : m_knots.Length;
        public float LengthInverse => m_lengthInverse;
        public int LUTCount { get; private set; }

        public event Action OnSplineKnotsUpdated;
        public event Action<ISpline> OnSplineTypeChanged;

        public ISplineKnot GetHeadKnot() => GetKnot(0);
        public ISplineKnot GetBackKnot() => GetKnot(m_knots.Length - 1);
        public ISplineKnot GetKnot(int knotIndex)
        {
            if (m_knots == null || m_knots.Length < 1) return null;
            return m_knots[knotIndex];
        }
        public int GetKnotIndex(SegmentedSplineKnot knot) => SplineRuntimeComponent.GetKnotIndex(this, knot);
        public bool ContainsKnot(SegmentedSplineKnot knot) => SplineRuntimeComponent.GetKnotIndex(this, knot) >= 0;
        public SegmentedSplineKnot GetKnotBefore(SegmentedSplineKnot knot)
        {
            int knotIndex = GetKnotIndex(knot) - 1;
            if (knotIndex < 0) return null;
            return m_knots[knotIndex];
        }
        public SegmentedSplineKnot GetKnotAfter(SegmentedSplineKnot knot)
        {
            int knotIndex = GetKnotIndex(knot) + 1;
            if (knotIndex >= m_knots.Length) return null;
            return m_knots[knotIndex];
        }

        public MetadataGroups GetSurfaceMetadata() => m_metadata;
        public SurfaceKnotMetadata GetKnotMetadataFromDistance(float distance) => SplineRuntimeComponent.GetKnotMetadataFromDistance(this, distance);
        public SurfaceKnotMetadata GetKnotMetadataFromKnotT(KnotT knotT) => SplineRuntimeComponent.GetKnotMetadataFromKnotT(this, knotT);
        public SurfaceKnotMetadata GetKnotMetadataFromLinearRatio(LinearRatio linearRatio) => SplineRuntimeComponent.GetKnotMetadataFromLinearRatio(this, linearRatio);
        public SplineLocation FindAdjacentSplineLocation(Vector3 worldPosition, Vector3 forward)
        {
            return SplineRuntimeComponent.FindAdjacentSplineLocation(this, this, transform, worldPosition, forward,
                (position, forwardVector, i) => SegmentedSplineRuntimeComponent.FindAdjacentKnotPosition(this, position, forwardVector, i));
        }
        // 0x06000564..566: both original nearest wrappers discard the plane argument;
        // the overload also ignores startKnot, despite retaining its original optional default.
        public SplineLocation FindNearestSplineLocation(Vector3 worldPosition) => FindNearestSplineLocation(worldPosition, null, -1);
        public SplineLocation FindNearestSplineLocation(Vector3 worldPosition, Vector3? planeNormal, int startKnot = -1)
        {
            Vector3 localPosition = transform.worldToLocalMatrix.MultiplyPoint3x4(worldPosition);
            SplineRuntimeComponent.FindNearestSplinePosition(this, localPosition, out Vector3 splinePosition, out LinearRatio linearRatio);
            SurfaceKnotMetadata metadata = GetKnotMetadataFromLinearRatio(linearRatio);
            return new SplineLocation { m_spline = this, m_knotLinearRatio = linearRatio, m_metadata = metadata };
        }
        public void FindNearestKnotPosition(Vector3 position, Vector3? planeNormal, int knotIndex, out Vector3 knotPosition, out KnotT knotT, out float sqrDistance)
            => SegmentedSplineRuntimeComponent.FindNearestKnotPosition(this, position, null, knotIndex, out knotPosition, out knotT, out sqrDistance);
        public SplineLocation GetSplineLocationFromDistance(float distance)
        {
            SegmentedSplineRuntimeComponent.GetSplineLocationFromDistance(this, distance, out LinearRatio linearRatio);
            SurfaceKnotMetadata metadata = GetKnotMetadataFromLinearRatio(linearRatio);
            return new SplineLocation { m_spline = this, m_knotLinearRatio = linearRatio, m_metadata = metadata };
        }

        public List<ISplineRuntimeHandle.KnotBounds> KnotBoundsList
        {
            get
            {
                if (m_knotBoundsList == null || m_knotBoundsList.Count != KnotCount - 1)
                    m_knotBoundsList = SplineRuntimeComponent.CreateKnotBounds(this);
                return m_knotBoundsList;
            }
        }
        public SplineControlPoints GetControlsForKnots(int knot0Index, int knot1Index) => SplineRuntimeComponent.GetControlsForKnots(this, knot0Index, knot1Index);
        public LinearRatio GetLinearRatioFromKnotT(KnotT knotT) => new LinearRatio(knotT.KnotIndex, knotT.T);
        public KnotT GetKnotTFromLinearRatio(LinearRatio linearRatio) => new KnotT(linearRatio.KnotIndex, linearRatio.T);

        // 0x0600056a..571: segmented coordinates repack the same index/T; they do not apply LUT conversion.
        public Vector3 GetLocalPosFromKnotT(KnotT knotT) => SegmentedSplineRuntimeComponent.PointAlongSpline(this, knotT);
        public LightweightTransform GetLocalTransformFromKnotT(KnotT knotT) => SegmentedSplineRuntimeComponent.PointAndRotationAlongSpline(this, knotT);
        public PositionAndTangent GetLocalPosAndTangentFromKnotT(KnotT knotT) => SegmentedSplineRuntimeComponent.PointAndTangentAlongSpline(this, knotT);
        public Vector3 GetLocalPosFromLinearRatio(LinearRatio linearRatio) => SegmentedSplineRuntimeComponent.PointAlongSpline(this, new KnotT(linearRatio.KnotIndex, linearRatio.T));
        public LightweightTransform GetLocalTransformFromLinearRatio(LinearRatio linearRatio) => SegmentedSplineRuntimeComponent.PointAndRotationAlongSpline(this, new KnotT(linearRatio.KnotIndex, linearRatio.T));
        public PositionAndTangent GetLocalPosAndTangentFromLinearRatio(LinearRatio linearRatio) => SegmentedSplineRuntimeComponent.PointAndTangentAlongSpline(this, new KnotT(linearRatio.KnotIndex, linearRatio.T));

        public LightweightTransform GetWorldTransformFromDistance(float distance)
        {
            LightweightTransform localTransform = GetSplineLocationFromDistance(distance).GetLocalTransform();
            Vector3 position = transform.localToWorldMatrix.MultiplyPoint3x4(localTransform.Location);
            Quaternion rotation = transform.rotation * localTransform.Orientation;
            return new LightweightTransform(position, rotation);
        }
        public LightweightTransform GetWorldTransformFromSplineT(float splineT) => GetWorldTransformFromDistance(m_length * splineT);
        public SurfaceLocation FindNearestSurfaceLocationFromWorld(Vector3 worldPosition) => SplineRuntimeComponent.GetSurfaceLocation(FindNearestSplineLocation(worldPosition), transform);
        public SurfaceLocation FindNearestSurfaceLocationFromLocal(Vector3 localPosition) => SplineRuntimeComponent.GetSurfaceLocation(GetSplineLocationFromDistance(localPosition.z), transform);

        public void KnotsUpdated()
        {
            m_timestamp = DateTime.Now;
            OnSplineKnotsUpdated?.Invoke();
        }
        public void UpdateSplineType(ISpline newSpline)
        {
            m_timestamp = DateTime.Now;
            OnSplineTypeChanged?.Invoke(newSpline);
        }
        public Vector3 TransformPoint(Vector3 point) => transform.TransformPoint(point);
        public Vector3 InverseTransformPoint(Vector3 point) => transform.InverseTransformPoint(point);
        public bool IsActive() => gameObject.activeInHierarchy;

        public DateTime GetTimestamp() => m_timestamp;

        // 0x0600057c..585 retain the distinct explicit contracts, including the original throw.
        ISegmentedSplineKnotRuntimeHandle ISegmentedSplineRuntimeHandle.GetKnotRuntimeHandle(int knotIndex)
        {
            if (KnotCount < 1) return null;
            return m_knots[knotIndex];
        }
        int ISegmentedSplineRuntimeHandle.GetKnotRuntimeHandleIndex(ISegmentedSplineKnotRuntimeHandle knotHandle) => GetKnotIndex(knotHandle as SegmentedSplineKnot);
        SplineControlPoints ISplineRuntimeHandle.GetControlsForKnots(int knot0Index, int knot1Index)
            => SplineRuntimeComponent.GetControlsForKnots(this, knot0Index, knot1Index);
        LinearRatio ISplineRuntimeHandle.GetLinearRatioFromKnotT(KnotT knotT) => new LinearRatio(knotT.KnotIndex, knotT.T);
        KnotT ISplineRuntimeHandle.GetKnotTFromLinearRatio(LinearRatio linearRatio) => new KnotT(linearRatio.KnotIndex, linearRatio.T);
        PositionAndTangent ISplineRuntimeHandle.GetLocalPosAndTangentFromKnotT(KnotT knotT)
            => SegmentedSplineRuntimeComponent.PointAndTangentAlongSpline(this, knotT);
        void ISplineRuntimeHandle.RecalculateLength(bool force) => throw new NotImplementedException();
        ISplineKnotRuntimeHandle ISplineRuntimeHandle.GetKnotRuntimeHandle(int knotIndex)
        {
            if (KnotCount < 1) return null;
            return m_knots[knotIndex];
        }
        int ISplineRuntimeHandle.GetKnotRuntimeHandleIndex(ISplineKnotRuntimeHandle knotHandle) => GetKnotIndex(knotHandle as SegmentedSplineKnot);
        void ISplineRuntimeHandle.FindNearestKnotPosition(Vector3 localPosition, int knotIndex, out Vector3 knotPosition, out KnotT knotT)
            => SegmentedSplineRuntimeComponent.FindNearestKnotPosition(this, localPosition, null, knotIndex, out knotPosition, out knotT, out float sqrDistance);
        // 0x06000586: no derived initializer; the base performs the original initialization.
        public SegmentedSpline() { }
    }
}
