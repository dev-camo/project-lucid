using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLSplines.Runtime 02000073. Both native architectures retain
    // parent-change caching and three separate owner-property reads on recalc.
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [ExecuteInEditMode]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SplineKnot : MonoBehaviour, ISplineKnot, ISplineKnotRuntimeHandle, ISplineKnotHandle
    {
        [SerializeField] private Vector3 m_localControlPointNext = Vector3.forward;
        [SerializeField] private Vector3 m_localControlPointPrev = -Vector3.forward;
        [SerializeField] private float[] m_tLUT;
        [SerializeField] private float m_length;
        [SerializeField] private MetadataGroups m_metadata;
        [SerializeField, HideInInspector] private bool m_lengthDirty = true;
        private Transform m_parentTransform;
        private EditableSpline m_parentSpline;

        // Original 060002f9: Unity inequality compares the cached parent first;
        // the subsequent null-conditional uses the CLR reference check.
        public EditableSpline EditableSpline
        {
            get
            {
                if (m_parentTransform != transform.parent)
                {
                    m_parentTransform = transform.parent;
                    m_parentSpline = m_parentTransform?.GetComponent<EditableSpline>();
                }
                return m_parentSpline;
            }
        }
        // Original 060002fa..06000305: local transform, unit-scale helper,
        // unnormalised tangents and an unguarded reciprocal are preserved.
        public LightweightTransform Transform => new LightweightTransform(transform.localPosition, transform.localRotation);
        public MetadataGroups Metadata => m_metadata;
        public Vector3 UpVector => Transform.Up;
        public Vector3 LocalControlPointNext => m_localControlPointNext;
        public Vector3 WorldControlPointNext => SplineKnotRuntimeComponent.TransformPositionLocalToWorld(this, m_localControlPointNext);
        public Vector3 LocalControlPointPrev => m_localControlPointPrev;
        public Vector3 WorldControlPointPrev => SplineKnotRuntimeComponent.TransformPositionLocalToWorld(this, m_localControlPointPrev);
        public Vector3 TangentNext => WorldControlPointNext - Transform.Location;
        public Vector3 TangentPrev => WorldControlPointPrev - Transform.Location;
        public float Length => m_length;
        public float LengthInverse => 1f / m_length;
        public float[] TLUT => m_tLUT;
        // Original 06000306..06000309.
        public void Initialise(int lutCount) { m_tLUT = new float[unchecked(lutCount + 1)]; }
        public float GetLUTValue(int index) => SplineKnotRuntimeComponent.GetLUTValue(this, index);
        public void SetLengthDirty() { m_lengthDirty = true; }
        public bool IsLengthDirty() => m_lengthDirty;
        // Original 0600030a: retain the repeated live EditableSpline reads.
        public void RecalculateLength()
        {
            m_length = SplineRuntimeComponent.RecalculateKnotLength(EditableSpline, EditableSpline.GetKnotIndex(this), ref m_tLUT, EditableSpline.LUTCount);
            m_lengthDirty = false;
        }
        // Original 0600030b initializers run before MonoBehaviour construction.
        public SplineKnot() { }
    }
}
