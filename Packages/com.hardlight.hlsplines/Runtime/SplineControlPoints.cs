using UnityEngine;

namespace Hardlight
{
    // Original HLSplines.Runtime value layout (0200008d); no original methods.
    public struct SplineControlPoints
    {
        public Vector3 m_controlPt0;
        public Vector3 m_controlPt1;
        public Vector3 m_controlPt2;
        public Vector3 m_controlPt3;
        public bool m_cacheSet;
        public Vector3 m_cacheVector1;
        public Vector3 m_cacheVector2;
        public Vector3 m_cacheVector3;
        public Vector3 m_cacheVector4;
    }
}
