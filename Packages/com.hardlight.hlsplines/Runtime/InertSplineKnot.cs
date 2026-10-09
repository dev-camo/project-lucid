using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLSplines.Runtime 0200009e. Complete native bodies on both architectures.
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class InertSplineKnot : ISplineKnot, IInertSplineKnotRuntimeHandle, ISplineKnotRuntimeHandle, ISplineKnotHandle
    {
        [SerializeField] private LightweightTransform m_transform;
        [SerializeField] private Vector3 m_localControlPointNext;
        [SerializeField] private Vector3 m_worldControlPointNext;
        [SerializeField] private Vector3 m_localControlPointPrev;
        [SerializeField] private Vector3 m_worldControlPointPrev;
        [SerializeField] private Vector3 m_tangentNext;
        [SerializeField] private Vector3 m_tangentPrev;
        [SerializeField] private float[] m_tLut;
        [SerializeField] private float m_length;
        [SerializeField] private float m_lengthInverse;
        [SerializeField] private MetadataGroups m_metadata;

        // Original 0600049f..060004a9. The original world getters also read
        // the local fields; the separately serialized world fields stay unused.
        public LightweightTransform Transform => m_transform;
        public MetadataGroups Metadata => m_metadata;
        public Vector3 LocalControlPointPrev => m_localControlPointPrev;
        public Vector3 WorldControlPointPrev => m_localControlPointPrev;
        public Vector3 LocalControlPointNext => m_localControlPointNext;
        public Vector3 WorldControlPointNext => m_localControlPointNext;
        public Vector3 TangentNext => m_tangentNext;
        public Vector3 TangentPrev => m_tangentPrev;
        public float Length => m_length;
        public float LengthInverse => m_lengthInverse;
        public float[] TLUT => m_tLut;
        // Original 060004aa: copy the selected editable knot, retain metadata
        // aliasing and use its live LUT length at each loop condition.
        public void Initialise(EditableSpline originalEditableSpline, int knotIndex)
        {
            SplineKnot knot = originalEditableSpline.GetKnot(knotIndex) as SplineKnot;
            m_transform = knot.Transform;
            m_length = knot.Length;
            m_lengthInverse = knot.LengthInverse;
            m_localControlPointNext = knot.LocalControlPointNext;
            m_worldControlPointNext = knot.LocalControlPointNext;
            m_localControlPointPrev = knot.LocalControlPointPrev;
            m_worldControlPointPrev = knot.LocalControlPointPrev;
            m_tangentNext = knot.TangentNext;
            m_tangentPrev = knot.TangentPrev;
            m_metadata = knot.Metadata;
            m_tLut = new float[knot.TLUT.Length];
            for (int i = 0; i < knot.TLUT.Length; i++) m_tLut[i] = knot.GetLUTValue(i);
        }
        // Original 060004ab..060004b1. These three interface throws are genuine
        // shipping methods, not unresolved reconstruction placeholders.
        public float GetLUTValue(int index) => SplineKnotRuntimeComponent.GetLUTValue(this, index);
        float ISplineKnotHandle.GetLUTValue(int index) => GetLUTValue(index);
        bool ISplineKnotRuntimeHandle.IsLengthDirty() => throw new NotImplementedException();
        void ISplineKnotRuntimeHandle.RecalculateLength() => throw new NotImplementedException();
        void ISplineKnotRuntimeHandle.SetLengthDirty() => throw new NotImplementedException();
        public void SetTransform(Vector3 location, Quaternion orientation) { m_transform = new LightweightTransform(location, orientation); }
        public InertSplineKnot() { }
    }
}
