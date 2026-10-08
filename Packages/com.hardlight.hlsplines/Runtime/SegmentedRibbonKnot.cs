using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SegmentedRibbonKnot
    {
        [SerializeField] private SegmentedRibbonSubKnot[] m_subKnots;
        private readonly Dictionary<RibbonAlignment, SegmentedRibbonSubKnot> m_cachedSubKnots =
            new Dictionary<RibbonAlignment, SegmentedRibbonSubKnot>(HLSplinesEnumComparers.RibbonAlignmentComparer);

        public SegmentedRibbonSubKnot this[RibbonAlignment alignment] => GetSubKnot(alignment);
        public LightweightTransform Transform => GetSubKnot(RibbonAlignment.Center).Transform;
        public MetadataGroups Metadata => GetSubKnot(RibbonAlignment.Center).Metadata;
        public Vector3 UpVector => GetSubKnot(RibbonAlignment.Center).Transform.Up;
        public Vector3 LocalControlPointNext => GetSubKnot(RibbonAlignment.Center).LocalControlPointNext;
        public Vector3 WorldControlPointNext => GetSubKnot(RibbonAlignment.Center).WorldControlPointNext;
        public Vector3 LocalControlPointPrev => GetSubKnot(RibbonAlignment.Center).LocalControlPointPrev;
        public Vector3 WorldControlPointPrev => GetSubKnot(RibbonAlignment.Center).WorldControlPointPrev;
        public Vector3 TangentNext => GetSubKnot(RibbonAlignment.Center).TangentNext;
        public Vector3 TangentPrev => GetSubKnot(RibbonAlignment.Center).TangentPrev;
        public float Length => GetSubKnot(RibbonAlignment.Center).Length;
        public float LengthInverse => GetSubKnot(RibbonAlignment.Center).LengthInverse;

        public void Initialise(EditableRibbon originalRibbon, int knotIndex, Matrix4x4 worldToLocal)
        {
            originalRibbon.Knots[knotIndex].GetSubKnots(out RibbonSubKnot[] originalKnots);
            m_subKnots = new SegmentedRibbonSubKnot[originalKnots.Length];
            m_cachedSubKnots.Clear();
            for (int i = 0; i < m_subKnots.Length; i++)
            {
                m_subKnots[i] = new SegmentedRibbonSubKnot();
                m_subKnots[i].Initialise(originalRibbon.GetSubSpline(originalKnots[i].Alignment), knotIndex);
            }
        }

        public SegmentedRibbonSubKnot GetSubKnot(RibbonAlignment alignment)
        {
            if (!m_cachedSubKnots.TryGetValue(alignment, out SegmentedRibbonSubKnot subKnot))
            {
                if (m_subKnots != null && m_subKnots.Length > 0)
                {
                    foreach (SegmentedRibbonSubKnot candidate in m_subKnots)
                    {
                        if (candidate.Alignment == alignment)
                        {
                            m_cachedSubKnots.Add(alignment, candidate);
                            subKnot = candidate;
                            break;
                        }
                    }
                }
            }
            return subKnot;
        }

        public SegmentedRibbonKnot() { }
    }
}
