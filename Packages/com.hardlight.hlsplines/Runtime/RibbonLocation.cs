using UnityEngine;

namespace Hardlight
{
    // Original HLSplines.Runtime 0x02000089; all six original public fields.
    public struct RibbonLocation
    {
        public IRibbon m_ribbon;
        public RibbonAlignment m_alignment;
        public LinearRatio m_knotLinearRatio;
        public Vector3 m_localOffset;
        public PositionBoundsInfo m_positionBoundsInfo;
        public SurfaceKnotMetadata m_metadata;

        // 0x06000357: null ribbon faults before zero-count handling; T reload follows Length.
        public float KnotDistance
        {
            get
            {
                if (m_ribbon.KnotCount == 0) return 0f;
                return m_ribbon.GetKnot(m_knotLinearRatio.KnotIndex, m_alignment).Length * m_knotLinearRatio.T;
            }
        }

        // 0x06000358: no captured count/index/ribbon; original callback reloads are retained.
        public float RibbonDistance
        {
            get
            {
                if (m_ribbon.KnotCount == 0) return 0f;
                float distance = 0f;
                for (int i = 0; i < m_knotLinearRatio.KnotIndex; ++i)
                    distance += m_ribbon.GetKnot(i, m_alignment).Length;
                return distance + KnotDistance;
            }
        }

        // 0x06000359: m_localOffset is loaded after the genuine ribbon callback returns.
        public LightweightTransform GetLocalTransform(bool ignoreOffset = false)
        {
            LightweightTransform result = m_ribbon.GetLocalTransformFromLinearRatio(m_knotLinearRatio, m_alignment);
            if (!ignoreOffset) result.Location += result.Orientation * m_localOffset;
            return result;
        }
    }
}
