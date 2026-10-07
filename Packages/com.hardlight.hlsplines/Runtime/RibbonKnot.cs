using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [ExecuteInEditMode]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class RibbonKnot : MonoBehaviour
    {
        [SerializeField] private bool m_overrideSubKnotOffset;
        [SerializeField] private bool m_overrideSubKnotHandles;
        [SerializeField] private RibbonSubKnot m_mainSubKnot;
        [SerializeField] private RibbonSubKnot m_leftSubKnot;
        [SerializeField] private RibbonSubKnot m_rightSubKnot;
        [SerializeField] private MetadataGroups m_metadata;
        private Transform m_parentTransform;
        private EditableRibbon m_parentSpline;
        private RibbonSubKnot[] m_cachedAllSubKnots = new RibbonSubKnot[3];

        // Original HLSplines.Runtime 0x06000297.
        public RibbonSubKnot this[RibbonAlignment alignment] => GetSubKnot(alignment);
        // Original 0x06000298: Unity equality then ordinary reference-null dispatch.
        public EditableRibbon Ribbon
        {
            get
            {
                if (m_parentTransform != transform.parent)
                {
                    m_parentTransform = transform.parent;
                    m_parentSpline = m_parentTransform?.GetComponent<EditableRibbon>();
                }
                return m_parentSpline;
            }
        }
        public LightweightTransform Transform => m_mainSubKnot.Transform;
        public MetadataGroups Metadata => m_metadata;
        public Vector3 UpVector => Transform.Up;
        public Vector3 LocalControlPointNext => m_mainSubKnot.LocalControlPointNext;
        public Vector3 LocalControlPointPrev => m_mainSubKnot.LocalControlPointPrev;
        public Vector3 WorldControlPointNext => m_mainSubKnot.WorldControlPointNext;
        public Vector3 WorldControlPointPrev => m_mainSubKnot.WorldControlPointPrev;
        public Vector3 TangentNext => m_mainSubKnot.TangentNext;
        public Vector3 TangentPrev => m_mainSubKnot.TangentPrev;
        public float Length => m_mainSubKnot.Length;
        public float LengthInverse => m_mainSubKnot.LengthInverse;
        public float[] TLUT => m_mainSubKnot.TLUT;
        public bool OverrideSubKnotOffset => m_overrideSubKnotOffset;
        public bool OverrideSubKnotHandles => m_overrideSubKnotHandles;
        public float GetLUTValue(int index) => m_mainSubKnot.GetLUTValue(index);
        public void SetLengthDirty(RibbonAlignment alignment) => GetSubKnot(alignment).SetLengthDirty();
        public bool IsLengthDirty(RibbonAlignment alignment) => GetSubKnot(alignment).IsLengthDirty();
        public void RecalculateLength(RibbonAlignment alignment) => GetSubKnot(alignment).RecalculateLength();
        // Original 0x060002ab; error literal is decoded from slot0x32b0178.
        public RibbonSubKnot GetSubKnot(RibbonAlignment alignment)
        {
            switch (alignment)
            {
                case RibbonAlignment.Left: return m_leftSubKnot;
                case RibbonAlignment.Right: return m_rightSubKnot;
                case RibbonAlignment.Center: return m_mainSubKnot;
                default: throw new ArgumentOutOfRangeException(nameof(alignment), alignment, null);
            }
        }
        // Original 0x060002ac stores center/right/left and returns the existing array.
        public void GetSubKnots(out RibbonSubKnot[] allSubKnots)
        {
            m_cachedAllSubKnots[0] = m_mainSubKnot;
            m_cachedAllSubKnots[1] = m_rightSubKnot;
            m_cachedAllSubKnots[2] = m_leftSubKnot;
            allSubKnots = m_cachedAllSubKnots;
        }
        public void GetSubKnots(out RibbonSubKnot mainSubKnot, out RibbonSubKnot leftSubKnot, out RibbonSubKnot rightSubKnot)
        {
            mainSubKnot = m_mainSubKnot;
            leftSubKnot = m_leftSubKnot;
            rightSubKnot = m_rightSubKnot;
        }
        public void KnotUpdated() => m_parentSpline.KnotsUpdated();
        public void Initialise(int lutCount)
        {
            TryGetSubKnotFromCache(ref m_mainSubKnot, RibbonAlignment.Center, lutCount);
            TryGetSubKnotFromCache(ref m_leftSubKnot, RibbonAlignment.Left, lutCount);
            TryGetSubKnotFromCache(ref m_rightSubKnot, RibbonAlignment.Right, lutCount);
            m_mainSubKnot.Initialise(this);
            m_leftSubKnot.Initialise(this);
            m_rightSubKnot.Initialise(this);
        }
        public void SetWidthOnSubKnots()
        {
            m_mainSubKnot.SetWidth();
            m_leftSubKnot.SetWidth();
            m_rightSubKnot.SetWidth();
        }
        public bool IsInitialised() => m_mainSubKnot.Parent != null && m_leftSubKnot.Parent != null && m_rightSubKnot.Parent != null;
        private void TryGetSubKnotFromCache(ref RibbonSubKnot subKnot, RibbonAlignment alignment, int lutCount)
        {
            if (subKnot != null) return;
            if (m_cachedAllSubKnots != null)
            {
                RibbonSubKnot cached = m_cachedAllSubKnots[GetCacheIndexFromAlignment(alignment)];
                if (cached != null) { subKnot = cached; return; }
            }
            subKnot = new RibbonSubKnot(alignment, lutCount);
        }
        private static int GetCacheIndexFromAlignment(RibbonAlignment alignment)
        {
            switch (alignment)
            {
                case RibbonAlignment.Left: return 2;
                case RibbonAlignment.Center: return 0;
                case RibbonAlignment.Right: return 1;
                default: throw new ArgumentOutOfRangeException(nameof(alignment), alignment, null);
            }
        }
        // Original0x060002b4 is represented by the three-element field initializer.
        public RibbonKnot() { }
    }
}
