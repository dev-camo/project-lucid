using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLSplines.Runtime 0200009b: all twenty-two declared methods.
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class InertRibbonSubKnot : IRibbonKnot, IInertSplineKnotRuntimeHandle,
        ISplineKnotRuntimeHandle, ISplineKnotHandle
    {
        [SerializeField] private LightweightTransform m_transform;
        [SerializeField] private Vector3 m_localOffset;
        [SerializeField] private Vector3 m_localControlPointNext;
        [SerializeField] private Vector3 m_worldControlPointNext;
        [SerializeField] private Vector3 m_localControlPointPrev;
        [SerializeField] private Vector3 m_worldControlPointPrev;
        [SerializeField] private Vector3 m_tangentNext;
        [SerializeField] private Vector3 m_tangentPrev;
        [SerializeField] private float[] m_tLUT;
        [SerializeField] private float m_length;
        [SerializeField] private float m_lengthInverse;
        [SerializeField] private RibbonAlignment m_alignment;
        [SerializeField] private MetadataGroups m_metadata;

        public RibbonAlignment Alignment => m_alignment;
        public MetadataGroups Metadata => m_metadata;
        public LightweightTransform Transform => m_transform;
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
        public float[] TLUT => m_tLUT;

        // 0600043a copies the authored knot in this order. Both world-named controls
        // retain the original local-control reads; no transform or normalization is added.
        public void Initialise(RibbonSubSpline originalSpline, int knotIndex)
        {
            RibbonSubKnot knot = originalSpline.GetKnot(knotIndex) as RibbonSubKnot;
            m_alignment = knot.Alignment;
            m_metadata = knot.Metadata;
            m_transform = knot.Transform;
            m_localOffset = knot.LocalOffset;
            m_localControlPointNext = knot.LocalControlPointNext;
            m_worldControlPointNext = knot.LocalControlPointNext;
            m_localControlPointPrev = knot.LocalControlPointPrev;
            m_worldControlPointPrev = knot.LocalControlPointPrev;
            m_tangentNext = knot.TangentNext;
            m_tangentPrev = knot.TangentPrev;
            m_tLUT = new float[knot.TLUT.Length];
            for (int i = 0; i < knot.TLUT.Length; ++i)
                m_tLUT[i] = knot.GetLUTValue(i);
            m_length = knot.Length;
            m_lengthInverse = 1f / m_length;
        }

        public float GetLUTValue(int index) => SplineKnotRuntimeComponent.GetLUTValue(this, index);
        float ISplineKnotHandle.GetLUTValue(int index) => GetLUTValue(index);
        bool ISplineKnotRuntimeHandle.IsLengthDirty() => false;
        void ISplineKnotRuntimeHandle.RecalculateLength() { }
        void ISplineKnotRuntimeHandle.SetLengthDirty() { }

        // 06000440 changes the captured transform only; controls, tangents and caches remain.
        public void SetTransform(Vector3 location, Quaternion orientation)
        {
            m_transform.Location = location;
            m_transform.Orientation = orientation;
        }

        public InertRibbonSubKnot() { }
    }
}
