using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class RibbonSubKnot : IRibbonKnot, ISplineKnotRuntimeHandle, ISplineKnotHandle
    {
        [SerializeField] private Vector3 m_localOffset = Vector3.zero;
        [SerializeField] private Vector3 m_localControlPointNext = Vector3.forward;
        [SerializeField] private Vector3 m_localControlPointPrev = -Vector3.forward;
        [SerializeField] private float[] m_tLUT;
        [SerializeField] private float m_length;
        [SerializeField, HideInInspector] private bool m_lengthDirty = true;
        [SerializeField, HideInInspector] private RibbonAlignment m_alignment;
        private RibbonKnot m_parent;

        // Original HLSplines.Runtime 0x060002b5..0x060002b7.
        public RibbonKnot Parent => m_parent;
        public RibbonAlignment Alignment => m_alignment;
        public MetadataGroups Metadata => m_parent.Metadata;

        // Original 0x060002b8: rotation precedes local position, then LocalOffset reloads the parent.
        public LightweightTransform Transform
        {
            get
            {
                Transform transform = m_parent.transform;
                Quaternion rotation = transform.localRotation;
                Vector3 position = transform.localPosition;
                return new LightweightTransform(position + rotation * LocalOffset, rotation);
            }
        }

        // Original 0x060002b9/0x060002ba: non-center offsets can be dynamically supplied by the owner.
        public Vector3 LocalOffset
        {
            get
            {
                if (m_parent.OverrideSubKnotOffset || m_alignment == RibbonAlignment.Center)
                    return m_localOffset;
                return new Vector3(m_parent.Ribbon.GetSplineOffset(m_alignment), 0f, 0f);
            }
            set { m_localOffset = value; }
        }

        // Original 0x060002bb/0x060002bc.
        public Vector3 UpVector => Transform.Up;
        public EditableRibbon Ribbon => m_parent.Ribbon;

        // Original 0x060002bd/0x060002be.
        public Vector3 LocalControlPointNext
        {
            get
            {
                if (m_parent.OverrideSubKnotHandles || m_alignment == RibbonAlignment.Center)
                    return m_localControlPointNext;
                return m_parent.LocalControlPointNext;
            }
            set { m_localControlPointNext = value; }
        }

        // Original 0x060002bf/0x060002c0.
        public Vector3 WorldControlPointNext
        {
            get { return SplineKnotRuntimeComponent.TransformPositionLocalToWorld(this, LocalControlPointNext); }
            set { m_localControlPointNext = SplineKnotRuntimeComponent.TransformPositionWorldToLocal(this, value); }
        }

        // Original 0x060002c1/0x060002c2.
        public Vector3 LocalControlPointPrev
        {
            get
            {
                if (m_parent.OverrideSubKnotHandles || m_alignment == RibbonAlignment.Center)
                    return m_localControlPointPrev;
                return m_parent.LocalControlPointPrev;
            }
            set { m_localControlPointPrev = value; }
        }

        // Original 0x060002c3/0x060002c4.
        public Vector3 WorldControlPointPrev
        {
            get { return SplineKnotRuntimeComponent.TransformPositionLocalToWorld(this, LocalControlPointPrev); }
            set { m_localControlPointPrev = SplineKnotRuntimeComponent.TransformPositionWorldToLocal(this, value); }
        }

        // Original 0x060002c5/0x060002c6: the control position callback happens before the second transform load.
        public Vector3 TangentNext => WorldControlPointNext - Transform.Location;
        public Vector3 TangentPrev => WorldControlPointPrev - Transform.Location;
        // Original 0x060002c7..0x060002ca. No zero-length reciprocal repair.
        public float Length => m_length;
        public float LengthInverse => 1f / m_length;
        public float[] TLUT { get { return m_tLUT; } set { m_tLUT = value; } }

        // Original 0x060002cb/0x060002cc. The constructor allocates lutCount, not lutCount+1.
        public RibbonSubKnot(int lutCount) { m_tLUT = new float[lutCount]; }
        public RibbonSubKnot(RibbonAlignment alignment, int lutCount) : this(lutCount) { m_alignment = alignment; }

        // Original 0x060002cd/0x060002ce. Initialise always replaces the array after publishing the parent.
        public void Initialise(RibbonKnot parent) { m_parent = parent; }
        public void Initialise(RibbonKnot parent, int lutCount)
        {
            m_parent = parent;
            m_tLUT = new float[lutCount + 1];
        }

        // Original 0x060002cf..0x060002d2.
        public int GetLUTLength() { return m_tLUT.Length; }
        public float GetLUTValue(int index) { return SplineKnotRuntimeComponent.GetLUTValue(this, index); }
        public void SetLengthDirty() { m_lengthDirty = true; }
        public bool IsLengthDirty() { return m_lengthDirty; }

        // Original 0x060002d3: each owner lookup is repeated before the scalar result is published.
        public void RecalculateLength()
        {
            RibbonSubSpline spline = m_parent.Ribbon.GetSubSpline(m_alignment);
            int knotIndex = m_parent.Ribbon.GetKnotIndex(this);
            int lutCount = m_parent.Ribbon.LUTCount;
            m_length = SplineRuntimeComponent.RecalculateKnotLength(spline, knotIndex, ref m_tLUT, lutCount);
            m_lengthDirty = false;
        }

        // Original 0x060002d4: the genuine Unity null test precedes the second owner lookup.
        public void SetWidth()
        {
            if (m_parent.Ribbon == null) return;
            m_localOffset = !m_parent.OverrideSubKnotOffset && m_alignment != RibbonAlignment.Center
                ? new Vector3(m_parent.Ribbon.GetSplineOffset(m_alignment), 0f, 0f) : m_localOffset;
        }
    }
}
