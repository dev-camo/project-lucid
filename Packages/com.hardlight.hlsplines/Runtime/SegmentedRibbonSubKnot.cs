using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SegmentedRibbonSubKnot : IRibbonKnot, ISegmentedSplineKnotRuntimeHandle,
        ISplineKnotRuntimeHandle, ISplineKnotHandle
    {
        [SerializeField] public SegmentSplinePoint[] m_segmentSplinePoints;
        [SerializeField] private LightweightTransform m_transform;
        [SerializeField] private Vector3 m_localOffset;
        [SerializeField] private Vector3 m_localControlPointNext;
        [SerializeField] private Vector3 m_worldControlPointNext;
        [SerializeField] private Vector3 m_localControlPointPrev;
        [SerializeField] private Vector3 m_worldControlPointPrev;
        [SerializeField] private Vector3 m_tangentNext;
        [SerializeField] private Vector3 m_tangentPrev;
        [SerializeField] private float m_length;
        [SerializeField] private float m_lengthInverse;
        [SerializeField] private RibbonAlignment m_alignment;
        [SerializeField] private MetadataGroups m_metadata;

        public RibbonAlignment Alignment => m_alignment;
        public LightweightTransform Transform => m_transform;
        public SegmentSplinePoint[] SegmentSplinePoints => m_segmentSplinePoints;
        public MetadataGroups Metadata => m_metadata;
        public Vector3 LocalOffset => m_localOffset;
        public Vector3 UpVector => m_transform.Up;
        public Vector3 LocalControlPointNext => m_localControlPointNext;
        public Vector3 WorldControlPointNext => m_worldControlPointNext;
        public Vector3 LocalControlPointPrev => m_localControlPointPrev;
        public Vector3 WorldControlPointPrev => m_worldControlPointPrev;
        public Vector3 TangentNext => m_tangentNext;
        public Vector3 TangentPrev => m_tangentPrev;
        public float Length => m_length;
        public float LengthInverse => m_lengthInverse;
        public float[] TLUT => throw new NotImplementedException();

        public void Initialise(RibbonSubSpline originalSpline, int knotIndex)
        {
            RibbonSubKnot originalKnot = originalSpline.GetKnot(knotIndex) as RibbonSubKnot;
            m_alignment = originalKnot.Alignment;
            m_metadata = originalKnot.Metadata;
            m_transform = originalKnot.Transform;
            m_localOffset = originalKnot.LocalOffset;
            m_localControlPointNext = originalKnot.LocalControlPointNext;
            // Original 0x06000521 copies each local control into its world field too.
            m_worldControlPointNext = originalKnot.LocalControlPointNext;
            m_localControlPointPrev = originalKnot.LocalControlPointPrev;
            m_worldControlPointPrev = originalKnot.LocalControlPointPrev;
            m_tangentNext = originalKnot.TangentNext;
            m_tangentPrev = originalKnot.TangentPrev;
            m_segmentSplinePoints = SegmentedSplineKnotRuntimeComponent.CalculateSegments(originalKnot, originalSpline, out m_length);
            m_lengthInverse = m_length > 0f ? 1f / m_length : 0f;
        }

        float ISplineKnotHandle.GetLUTValue(int index) => throw new NotImplementedException();
        bool ISplineKnotRuntimeHandle.IsLengthDirty() => throw new NotImplementedException();
        void ISplineKnotRuntimeHandle.RecalculateLength() => throw new NotImplementedException();
        void ISplineKnotRuntimeHandle.SetLengthDirty() => throw new NotImplementedException();

        public SegmentedRibbonSubKnot() { }
    }
}
