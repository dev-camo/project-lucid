using System;
using UnityEngine;

namespace Hardlight
{
    [Serializable]
    public struct SegmentSplinePoint
    {
        public Vector3 m_position;
        public Vector3 m_normalisedDirection;
        public float m_startDotDirection;
        public float m_endDotDirection;
        public float m_length;
        public float m_lengthInverse;
    }
}
