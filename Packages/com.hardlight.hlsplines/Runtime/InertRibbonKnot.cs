using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLSplines.Runtime 0200009a: all seventeen declared methods.
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class InertRibbonKnot
    {
        [SerializeField] private InertRibbonSubKnot[] m_subKnots;
        private readonly Dictionary<RibbonAlignment, InertRibbonSubKnot> m_cachedSubKnots =
            new Dictionary<RibbonAlignment, InertRibbonSubKnot>(HLSplinesEnumComparers.RibbonAlignmentComparer);

        public InertRibbonSubKnot this[RibbonAlignment alignment] => GetSubKnot(alignment);
        public LightweightTransform Transform => GetSubKnot(RibbonAlignment.Center).Transform;
        public MetadataGroups Metadata => GetSubKnot(RibbonAlignment.Center).Metadata;
        public Vector3 UpVector => GetSubKnot(RibbonAlignment.Center).UpVector;
        public Vector3 LocalControlPointNext => GetSubKnot(RibbonAlignment.Center).LocalControlPointNext;
        public Vector3 WorldControlPointNext => GetSubKnot(RibbonAlignment.Center).WorldControlPointNext;
        public Vector3 LocalControlPointPrev => GetSubKnot(RibbonAlignment.Center).LocalControlPointPrev;
        public Vector3 WorldControlPointPrev => GetSubKnot(RibbonAlignment.Center).WorldControlPointPrev;
        public Vector3 TangentNext => GetSubKnot(RibbonAlignment.Center).TangentNext;
        public Vector3 TangentPrev => GetSubKnot(RibbonAlignment.Center).TangentPrev;
        public float Length => GetSubKnot(RibbonAlignment.Center).Length;
        public float LengthInverse => GetSubKnot(RibbonAlignment.Center).LengthInverse;
        public float[] TLUT => GetSubKnot(RibbonAlignment.Center).TLUT;

        // 06000428 retains the original shared provider's upper/negative-index ordering.
        public float GetLUTValue(int index) => GetSubKnot(RibbonAlignment.Center).GetLUTValue(index);

        // 06000429 publishes the new array and clears the cache before initializing each entry.
        public void Initialise(EditableRibbon originalRibbon, int knotIndex)
        {
            originalRibbon.Knots[knotIndex].GetSubKnots(out RibbonSubKnot[] subKnots);
            m_subKnots = new InertRibbonSubKnot[subKnots.Length];
            m_cachedSubKnots.Clear();
            for (int i = 0; i < subKnots.Length; ++i)
            {
                m_subKnots[i] = new InertRibbonSubKnot();
                m_subKnots[i].Initialise(originalRibbon.GetSubSpline(subKnots[i].Alignment), knotIndex);
            }
        }

        // 0600042a caches only the first matching entry; misses and a missing array return null.
        public InertRibbonSubKnot GetSubKnot(RibbonAlignment alignment)
        {
            if (!m_cachedSubKnots.TryGetValue(alignment, out InertRibbonSubKnot subKnot) && m_subKnots != null)
            {
                foreach (InertRibbonSubKnot candidate in m_subKnots)
                {
                    if (candidate.Alignment == alignment)
                    {
                        m_cachedSubKnots.Add(alignment, candidate);
                        subKnot = candidate;
                        break;
                    }
                }
            }
            return subKnot;
        }

        public InertRibbonKnot() { }
    }
}
