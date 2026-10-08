using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SegmentedRibbonSubSpline : ISegmentedSplineRuntimeHandle, ISplineDrawerHandle, ISplineRuntimeHandle
    {
        [SerializeField] protected float m_length;
        [SerializeField] protected float m_lengthInverse;
        [SerializeField] private SegmentedRibbon m_parent;
        [SerializeField] private RibbonAlignment m_alignment;
        private List<ISplineRuntimeHandle.KnotBounds> m_knotBoundsList;

        public RibbonAlignment Alignment => m_alignment;
        public LightweightTransform Transform => m_parent.Transform;
        public SplineType SplineType => m_parent.GetSplineType(m_alignment);
        public int KnotCount => m_parent.KnotCount;
        public float Length => m_length;
        public float LengthInverse => m_lengthInverse;
        public int LUTCount { get; private set; }
        public MetadataGroups Metadata => m_parent.Metadata;
        public List<ISplineRuntimeHandle.KnotBounds> KnotBoundsList
        {
            get
            {
                if (m_knotBoundsList == null || m_knotBoundsList.Count != KnotCount - 1)
                    m_knotBoundsList = SplineRuntimeComponent.CreateKnotBounds(this);
                return m_knotBoundsList;
            }
        }

        public void Initialise(SegmentedRibbon parentRibbon, RibbonSubSpline originalSpline)
        {
            m_parent = parentRibbon;
            m_alignment = originalSpline.Alignment;
            m_length = 0f;
            for (int i = 0; i < KnotCount; i++) m_length += GetKnot(i).Length;
            m_lengthInverse = m_length > 0f ? 1f / m_length : 0f;
            LUTCount = originalSpline.LUTCount;
        }

        public IRibbonKnot GetKnot(int knotIndex)
        {
            SegmentedRibbonKnot[] knots = m_parent.Knots;
            if (knots == null || knots.Length < 1 || knotIndex < 0 || knotIndex >= knots.Length) return null;
            return knots[knotIndex].GetSubKnot(m_alignment);
        }
        public int GetKnotIndex(SegmentedRibbonSubKnot knot) => SplineRuntimeComponent.GetKnotIndex(this, knot);
        public SurfaceKnotMetadata GetKnotMetadataFromDistance(float distance) => SplineRuntimeComponent.GetKnotMetadataFromDistance(this, distance);
        public SurfaceKnotMetadata GetKnotMetadataFromKnotT(KnotT knotT) => SplineRuntimeComponent.GetKnotMetadataFromKnotT(this, knotT);
        public SurfaceKnotMetadata GetKnotMetadataFromLinearRatio(LinearRatio linearRatio) => SplineRuntimeComponent.GetKnotMetadataFromLinearRatio(this, linearRatio);
        public SplineControlPoints GetControlsForKnots(int knot0Index, int knot1Index) => SplineRuntimeComponent.GetControlsForKnots(this, knot0Index, knot1Index);
        public LinearRatio GetLinearRatioFromKnotT(KnotT knotT) => SegmentedSplineRuntimeComponent.GetLinearRatioFromKnotT(this, knotT);
        public KnotT GetKnotTFromLinearRatio(LinearRatio linearRatio) => SegmentedSplineRuntimeComponent.GetKnotTFromLinearRatio(this, linearRatio);
        public Vector3 GetLocalPosFromKnotT(KnotT knotT) => SegmentedSplineRuntimeComponent.PointAlongSpline(this, knotT);
        public LightweightTransform GetLocalTransformFromKnotT(KnotT knotT) => SegmentedSplineRuntimeComponent.PointAndRotationAlongSpline(this, knotT);
        public PositionAndTangent GetLocalPosAndTangentFromKnotT(KnotT knotT) => SegmentedSplineRuntimeComponent.PointAndTangentAlongSpline(this, knotT);
        public void FindNearestSplinePosition(Vector3 position, int startKnot, out Vector3 splinePosition, out LinearRatio knotLinearRatio) =>
            SplineRuntimeComponent.FindNearestSplinePosition(this, position, out splinePosition, out knotLinearRatio);
        public void FindNearestKnotPosition(Vector3 position, Vector3? planeNormal, int knotIndex, out Vector3 knotPosition, out KnotT knotT, out float sqrDistance) =>
            SegmentedSplineRuntimeComponent.FindNearestKnotPosition(this, position, planeNormal, knotIndex, out knotPosition, out knotT, out sqrDistance);
        public Vector3 GetLocalPosFromLinearRatio(LinearRatio linearRatio) => GetLocalPosFromKnotT(GetKnotTFromLinearRatio(linearRatio));
        public LightweightTransform GetLocalTransformFromLinearRatio(LinearRatio linearRatio) => GetLocalTransformFromKnotT(GetKnotTFromLinearRatio(linearRatio));
        public PositionAndTangent GetLocalPosAndTangentFromLinearRatio(LinearRatio linearRatio) => GetLocalPosAndTangentFromKnotT(GetKnotTFromLinearRatio(linearRatio));

        ISegmentedSplineKnotRuntimeHandle ISegmentedSplineRuntimeHandle.GetKnotRuntimeHandle(int knotIndex) => GetKnot(knotIndex) as ISegmentedSplineKnotRuntimeHandle;
        int ISegmentedSplineRuntimeHandle.GetKnotRuntimeHandleIndex(ISegmentedSplineKnotRuntimeHandle knotHandle) => GetKnotIndex(knotHandle as SegmentedRibbonSubKnot);
        SplineControlPoints ISplineRuntimeHandle.GetControlsForKnots(int knot0Index, int knot1Index) => GetControlsForKnots(knot0Index, knot1Index);
        LinearRatio ISplineRuntimeHandle.GetLinearRatioFromKnotT(KnotT knotT) => GetLinearRatioFromKnotT(knotT);
        KnotT ISplineRuntimeHandle.GetKnotTFromLinearRatio(LinearRatio linearRatio) => GetKnotTFromLinearRatio(linearRatio);
        PositionAndTangent ISplineRuntimeHandle.GetLocalPosAndTangentFromKnotT(KnotT knotT) => GetLocalPosAndTangentFromKnotT(knotT);
        void ISplineRuntimeHandle.RecalculateLength(bool force) => throw new NotImplementedException();
        ISplineKnotRuntimeHandle ISplineRuntimeHandle.GetKnotRuntimeHandle(int knotIndex) => GetKnot(knotIndex) as ISplineKnotRuntimeHandle;
        int ISplineRuntimeHandle.GetKnotRuntimeHandleIndex(ISplineKnotRuntimeHandle knotHandle) => GetKnotIndex(knotHandle as SegmentedRibbonSubKnot);
        void ISplineRuntimeHandle.FindNearestKnotPosition(Vector3 localPosition, int knotIndex, out Vector3 knotPosition, out KnotT knotT) =>
            FindNearestKnotPosition(localPosition, null, knotIndex, out knotPosition, out knotT, out float sqrDistance);

        public SegmentedRibbonSubSpline() { }
    }
}
