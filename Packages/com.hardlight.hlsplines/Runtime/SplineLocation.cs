namespace Hardlight
{
    // Original HLSplines.Runtime 0x06000362..364; field reloads follow interface calls.
    public struct SplineLocation
    {
        public ISpline m_spline;
        public LinearRatio m_knotLinearRatio;
        public SurfaceKnotMetadata m_metadata;

        public float KnotDistance
        {
            get
            {
                if (m_spline.KnotCount != 0)
                    return m_spline.GetKnot(m_knotLinearRatio.KnotIndex).Length * m_knotLinearRatio.T;
                return 0f;
            }
        }

        public float SplineDistance
        {
            get
            {
                if (m_spline.KnotCount == 0) return 0f;
                float distance = 0f;
                for (int knotIndex = 0; knotIndex < m_knotLinearRatio.KnotIndex; ++knotIndex)
                    distance += m_spline.GetKnot(knotIndex).Length;
                return distance + KnotDistance;
            }
        }

        public LightweightTransform GetLocalTransform()
        {
            return m_spline.GetLocalTransformFromLinearRatio(m_knotLinearRatio);
        }
    }
}
