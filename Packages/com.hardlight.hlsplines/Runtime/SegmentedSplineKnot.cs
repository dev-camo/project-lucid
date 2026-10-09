using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLSplines.Runtime 020000a9. Complete native bodies on both architectures.
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SegmentedSplineKnot : ISplineKnot, ISegmentedSplineKnotRuntimeHandle, ISplineKnotRuntimeHandle, ISplineKnotHandle
    {
        [SerializeField] public SegmentSplinePoint[] m_segmentSplinePoints;
        [SerializeField] public LightweightTransform m_transform;
        [SerializeField] private Vector3 m_localControlPointNext;
        [SerializeField] private Vector3 m_worldControlPointNext;
        [SerializeField] private Vector3 m_localControlPointPrev;
        [SerializeField] private Vector3 m_worldControlPointPrev;
        [SerializeField] private Vector3 m_tangentNext;
        [SerializeField] private Vector3 m_tangentPrev;
        [SerializeField] public float m_length;
        [SerializeField] public float m_lengthInverse;
        [SerializeField] public MetadataGroups m_metadata;

        // Original 06000588..06000593. The LUT getter genuinely throws.
        public LightweightTransform Transform => m_transform;
        public SegmentSplinePoint[] SegmentSplinePoints => m_segmentSplinePoints;
        public MetadataGroups Metadata => m_metadata;
        public Vector3 LocalControlPointNext => m_localControlPointNext;
        public Vector3 WorldControlPointNext => m_worldControlPointNext;
        public Vector3 LocalControlPointPrev => m_localControlPointPrev;
        public Vector3 WorldControlPointPrev => m_worldControlPointPrev;
        public Vector3 TangentNext => m_tangentNext;
        public Vector3 TangentPrev => m_tangentPrev;
        public float Length => m_length;
        public float LengthInverse => m_lengthInverse;
        public float[] TLUT => throw new NotImplementedException();
        // Original 06000594 preserves local copies in both world fields, then
        // computes segments before reading the freshly written length reciprocal.
        public void Initialise(EditableSpline originalEditableSpline, int knotIndex)
        {
            SplineKnot knot = originalEditableSpline.GetKnot(knotIndex) as SplineKnot;
            m_transform = knot.Transform;
            m_localControlPointNext = knot.LocalControlPointNext;
            m_worldControlPointNext = knot.LocalControlPointNext;
            m_localControlPointPrev = knot.LocalControlPointPrev;
            m_worldControlPointPrev = knot.LocalControlPointPrev;
            m_tangentNext = knot.TangentNext;
            m_tangentPrev = knot.TangentPrev;
            m_metadata = knot.Metadata;
            m_segmentSplinePoints = SegmentedSplineKnotRuntimeComponent.CalculateSegments(knot, originalEditableSpline, out m_length);
            m_lengthInverse = m_length > 0f ? 1f / m_length : 0f;
        }
        // Original 06000595..06000599: four actual throws and Object-only ctor.
        public float GetLUTValue(int index) => throw new NotImplementedException();
        public bool IsLengthDirty() => throw new NotImplementedException();
        public void RecalculateLength() => throw new NotImplementedException();
        public void SetLengthDirty() => throw new NotImplementedException();
        public SegmentedSplineKnot() { }
    }
}
