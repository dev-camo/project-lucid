using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLSplines.Runtime 0200009c: all thirty-seven declared methods.
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class InertRibbonSubSpline : IInertSplineRuntimeHandle, ISplineRuntimeHandle, ISplineDrawerHandle
    {
        [SerializeField] protected float m_length;
        [SerializeField] protected float m_lengthInverse;
        [SerializeField] private InertRibbon m_parent;
        [SerializeField] private RibbonAlignment m_alignment;
        private List<ISplineRuntimeHandle.KnotBounds> m_knotBoundsList;

        public LightweightTransform Transform => m_parent.Transform;
        public SplineType SplineType => m_parent.GetSplineType(m_alignment);
        public RibbonAlignment Alignment => m_alignment;
        public int KnotCount => m_parent.KnotCount;
        public float Length => m_length;
        public float LengthInverse => m_lengthInverse;
        public int LUTCount { get; private set; }
        public MetadataGroups Metadata => m_parent.GetSurfaceMetadata();
        public List<ISplineRuntimeHandle.KnotBounds> KnotBoundsList
        {
            get
            {
                if (m_knotBoundsList == null || m_knotBoundsList.Count != KnotCount - 1)
                    m_knotBoundsList = SplineRuntimeComponent.CreateKnotBounds(this);
                return m_knotBoundsList;
            }
        }

        // 0600044c retains the existing bounds cache; publication precedes every source read.
        public void Initialise(InertRibbon parentRibbon, RibbonSubSpline originalSpline)
        {
            m_parent = parentRibbon;
            m_alignment = originalSpline.Alignment;
            m_length = originalSpline.Length;
            m_lengthInverse = 1f / m_length;
            LUTCount = originalSpline.LUTCount;
        }

        public SurfaceKnotMetadata GetKnotMetadataFromDistance(float distance) =>
            SplineRuntimeComponent.GetKnotMetadataFromDistance(this, distance);
        public SurfaceKnotMetadata GetKnotMetadataFromKnotT(KnotT knotT) =>
            SplineRuntimeComponent.GetKnotMetadataFromKnotT(this, knotT);
        public SurfaceKnotMetadata GetKnotMetadataFromLinearRatio(LinearRatio linearRatio) =>
            SplineRuntimeComponent.GetKnotMetadataFromLinearRatio(this, linearRatio);
        public SplineControlPoints GetControlsForKnots(int knot0Index, int knot1Index) =>
            SplineRuntimeComponent.GetControlsForKnots(this, knot0Index, knot1Index);

        public IRibbonKnot GetKnot(int knotIndex) =>
            m_parent.KnotCount > 0 ? m_parent[knotIndex][m_alignment] : null;
        public int GetKnotIndex(InertRibbonSubKnot knot) => SplineRuntimeComponent.GetKnotIndex(this, knot);
        public LinearRatio GetLinearRatioFromKnotT(KnotT knotT) =>
            InertSplineRuntimeComponent.GetLinearRatioFromKnotT(this, knotT);
        public KnotT GetKnotTFromLinearRatio(LinearRatio linearRatio) =>
            InertSplineRuntimeComponent.GetKnotTFromLinearRatio(this, linearRatio);
        public void FindNearestKnotPosition(Vector3 position, int knotIndex,
            out Vector3 knotPosition, out KnotT knotT) =>
            SplineRuntimeComponent.FindNearestKnotPositionOnSpline(this, position, knotIndex, out knotPosition, out knotT);

        // 06000456 captures the requested controls before the independent magnitude local.
        public PositionAndTangent GetLocalPosAndTangentFromKnotT(KnotT knotT)
        {
            SplineControlPoints controls = GetControlsForKnots(knotT.KnotIndex, knotT.KnotIndex + 1);
            float tangentMagnitude = 0f;
            return SplineRuntimeComponent.PointAndTangentAlongSpline(this, ref tangentMagnitude, knotT.T, ref controls);
        }
        public LightweightTransform GetLocalTransformFromKnotT(KnotT knotT) =>
            SplineRuntimeComponent.PointAndRotationAlongSpline(this, knotT);
        public Vector3 GetLocalPosFromKnotT(KnotT knotT)
        {
            SplineControlPoints controls = GetControlsForKnots(knotT.KnotIndex, knotT.KnotIndex + 1);
            return SplineRuntimeComponent.PointAlongSpline(this, knotT.T, ref controls);
        }
        public PositionAndTangent GetLocalPosAndTangentFromLinearRatio(LinearRatio linearRatio) =>
            GetLocalPosAndTangentFromKnotT(GetKnotTFromLinearRatio(linearRatio));
        public LightweightTransform GetLocalTransformFromLinearRatio(LinearRatio linearRatio) =>
            GetLocalTransformFromKnotT(GetKnotTFromLinearRatio(linearRatio));
        public Vector3 GetLocalPosFromLinearRatio(LinearRatio linearRatio) =>
            GetLocalPosFromKnotT(GetKnotTFromLinearRatio(linearRatio));
        private ISplineKnotRuntimeHandle GetKnotRuntimeHandle(int knotIndex) =>
            GetKnot(knotIndex) as ISplineKnotRuntimeHandle;
        SplineControlPoints ISplineRuntimeHandle.GetControlsForKnots(int knot0Index, int knot1Index) =>
            GetControlsForKnots(knot0Index, knot1Index);
        LinearRatio ISplineRuntimeHandle.GetLinearRatioFromKnotT(KnotT knotT) => GetLinearRatioFromKnotT(knotT);
        KnotT ISplineRuntimeHandle.GetKnotTFromLinearRatio(LinearRatio linearRatio) => GetKnotTFromLinearRatio(linearRatio);
        PositionAndTangent ISplineRuntimeHandle.GetLocalPosAndTangentFromKnotT(KnotT knotT) =>
            GetLocalPosAndTangentFromKnotT(knotT);

        // 06000461 genuinely throws on both shipping CPUs; force is not inspected.
        void ISplineRuntimeHandle.RecalculateLength(bool force) => throw new NotImplementedException();
        ISplineKnotRuntimeHandle ISplineRuntimeHandle.GetKnotRuntimeHandle(int knotIndex) => GetKnotRuntimeHandle(knotIndex);
        int ISplineRuntimeHandle.GetKnotRuntimeHandleIndex(ISplineKnotRuntimeHandle knotHandle) =>
            GetKnotIndex(knotHandle as InertRibbonSubKnot);
        public void KnotsUpdated() => m_parent.KnotsUpdated();
        void ISplineRuntimeHandle.FindNearestKnotPosition(Vector3 localPosition, int knotIndex,
            out Vector3 knotPosition, out KnotT knotT) =>
            FindNearestKnotPosition(localPosition, knotIndex, out knotPosition, out knotT);

        public InertRibbonSubSpline() { }
    }
}
