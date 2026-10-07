using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class RibbonSubSpline : ISplineRuntimeHandle
    {
        [SerializeField] private RibbonAlignment m_alignment;
        [SerializeField] protected float m_length;
        private List<ISplineRuntimeHandle.KnotBounds> m_knotBoundsList;
        private EditableRibbon m_parent;

        // Original HLSplines.Runtime 0x060002d5: compare the populated cache length with current count-1.
        public List<ISplineRuntimeHandle.KnotBounds> KnotBoundsList
        {
            get
            {
                if (m_knotBoundsList == null || m_knotBoundsList.Count != m_parent.KnotCount - 1)
                    m_knotBoundsList = SplineRuntimeComponent.CreateKnotBounds(this);
                return m_knotBoundsList;
            }
        }

        // Original 0x060002d6: retrieve the owner's transform twice, local position before rotation.
        public LightweightTransform Transform => new LightweightTransform(m_parent.transform.localPosition, m_parent.transform.localRotation);
        // Original 0x060002d7..0x060002db.
        public SplineType SplineType => m_parent.GetSplineType(m_alignment);
        public RibbonAlignment Alignment => m_alignment;
        public int KnotCount => m_parent.KnotCount;
        public float Length => m_length;
        public float LengthInverse => 1f / m_length;
        // Original 0x060002dc/0x060002dd: one genuine compiler-generated backing field.
        public int LUTCount { get; private set; }
        // Original 0x060002de.
        public MetadataGroups Metadata => m_parent.Metadata;

        // Original 0x060002df/0x060002e0.
        public RibbonSubSpline(RibbonAlignment alignment) { m_alignment = alignment; }
        public void Initialise(EditableRibbon parent) { m_parent = parent; LUTCount = parent.LUTCount; }
        // Original 0x060002e1..0x060002e4.
        public SurfaceKnotMetadata GetKnotMetadataFromDistance(float distance) => SplineRuntimeComponent.GetKnotMetadataFromDistance(this, distance);
        public SurfaceKnotMetadata GetKnotMetadataFromKnotT(KnotT knotT) => SplineRuntimeComponent.GetKnotMetadataFromKnotT(this, knotT);
        public SurfaceKnotMetadata GetKnotMetadataFromLinearRatio(LinearRatio linearRatio) => SplineRuntimeComponent.GetKnotMetadataFromLinearRatio(this, linearRatio);
        public SplineControlPoints GetControlsForKnots(int knot0Index, int knot1Index) => SplineRuntimeComponent.GetControlsForKnots(this, knot0Index, knot1Index);

        // Original 0x060002e5/0x060002e6: an empty knot list returns null; the real cast is an as operation.
        public IRibbonKnot GetKnot(int knotIndex)
        {
            if (KnotCount < 1) return null;
            return m_parent.GetKnot(knotIndex, m_alignment);
        }
        private ISplineKnotRuntimeHandle GetKnotRuntimeHandle(int knotIndex) => GetKnot(knotIndex) as ISplineKnotRuntimeHandle;
        // Original 0x060002e7/0x060002e8/0x060002e9.
        public int GetKnotIndex(RibbonSubKnot knot) => SplineRuntimeComponent.GetKnotIndex(this, knot);
        public LinearRatio GetLinearRatioFromKnotT(KnotT knotT) => SplineRuntimeComponent.GetLinearRatioFromKnotT(GetKnotRuntimeHandle(knotT.KnotIndex), knotT);
        public KnotT GetKnotTFromLinearRatio(LinearRatio linearRatio) => SplineRuntimeComponent.GetKnotTFromLinearRatio(this, linearRatio);

        // Original 0x060002ea: controls callback precedes the zero magnitude local and the point/tangent call.
        public PositionAndTangent GetLocalPosAndTangentFromKnotT(KnotT knotT)
        {
            SplineControlPoints controls = SplineRuntimeComponent.GetControlsForKnots(this, knotT.KnotIndex, knotT.KnotIndex + 1);
            float tangentMagnitude = 0f;
            return SplineRuntimeComponent.PointAndTangentAlongSpline(this, ref tangentMagnitude, knotT.T, ref controls);
        }
        // Original 0x060002eb/0x060002ec.
        public LightweightTransform GetLocalTransformFromKnotT(KnotT knotT) => SplineRuntimeComponent.PointAndRotationAlongSpline(this, knotT);
        public Vector3 GetLocalPosFromKnotT(KnotT knotT)
        {
            SplineControlPoints controls = SplineRuntimeComponent.GetControlsForKnots(this, knotT.KnotIndex, knotT.KnotIndex + 1);
            return SplineRuntimeComponent.PointAlongSpline(this, knotT.T, ref controls);
        }
        // Original 0x060002ed..0x060002ef: scalar conversion occurs before querying geometry.
        public PositionAndTangent GetLocalPosAndTangentFromLinearRatio(LinearRatio linearRatio) => GetLocalPosAndTangentFromKnotT(GetKnotTFromLinearRatio(linearRatio));
        public LightweightTransform GetLocalTransformFromLinearRatio(LinearRatio linearRatio) => GetLocalTransformFromKnotT(GetKnotTFromLinearRatio(linearRatio));
        public Vector3 GetLocalPosFromLinearRatio(LinearRatio linearRatio) => GetLocalPosFromKnotT(GetKnotTFromLinearRatio(linearRatio));

        // Original 0x060002f0: clear the bounds cache before length callbacks; publish only after success.
        public void RecalculateLength(bool force = false)
        {
            m_knotBoundsList = null;
            m_length = SplineRuntimeComponent.RecalculateKnotsLength(this, force);
        }
        // Original 0x060002f1..0x060002f3: preserve the original explicit interface methods and as cast.
        ISplineKnotRuntimeHandle ISplineRuntimeHandle.GetKnotRuntimeHandle(int knotIndex) => GetKnot(knotIndex) as ISplineKnotRuntimeHandle;
        int ISplineRuntimeHandle.GetKnotRuntimeHandleIndex(ISplineKnotRuntimeHandle knotHandle) => GetKnotIndex(knotHandle as RibbonSubKnot);
        void ISplineRuntimeHandle.FindNearestKnotPosition(Vector3 position, int knotIndex, out Vector3 knotPosition, out KnotT knotT)
        {
            SplineRuntimeComponent.FindNearestKnotPositionOnSpline(this, position, knotIndex, out knotPosition, out knotT);
        }
        // Original 0x060002f4: capture count once, then reload the parent's list/alignment for each item.
        private bool AreKnotsLengthDirty()
        {
            int count = KnotCount;
            for (int i = 0; i < count; i++)
                if (m_parent.Knots[i][m_alignment].IsLengthDirty()) return true;
            return false;
        }
    }
}
