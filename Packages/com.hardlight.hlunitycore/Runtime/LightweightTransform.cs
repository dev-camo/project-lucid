using System;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000192; complete twelve-method value type.
    [Serializable]
    public struct LightweightTransform
    {
        [SerializeField] private Vector3 m_location;
        [SerializeField] private Quaternion m_orientation;

        // 0x06000b3a: position is read and stored before rotation.
        public LightweightTransform(Transform fromTransform)
        {
            this = default;
            m_location = fromTransform.position;
            m_orientation = fromTransform.rotation;
        }

        // 0x06000b3b: raw values, with no quaternion normalization.
        public LightweightTransform(Vector3 pos, Quaternion rot)
        {
            m_location = pos;
            m_orientation = rot;
        }

        public Vector3 Location { get => m_location; set => m_location = value; }
        public Quaternion Orientation { get => m_orientation; set => m_orientation = value; }
        public Vector3 Forwards => m_orientation * Vector3.forward;
        public Vector3 Right => m_orientation * Vector3.right;
        public Vector3 Up => m_orientation * Vector3.up;

        // 0x06000b43: Vector3.Lerp clamps; Quaternion.Lerp receives the original factor.
        public static LightweightTransform Lerp(LightweightTransform from, LightweightTransform to, float factor)
        {
            return new LightweightTransform(Vector3.Lerp(from.m_location, to.m_location, factor),
                Quaternion.Lerp(from.m_orientation, to.m_orientation, factor));
        }

        // 0x06000b44: location publication precedes snap/ordered-positive rotation paths.
        public void SmoothTowards(LightweightTransform target, float maxPositionBlend, float maxRotation, bool snapRotation)
        {
            m_location = Vector3.MoveTowards(m_location, target.m_location, maxPositionBlend);
            if (snapRotation)
                m_orientation = target.m_orientation;
            else if (maxRotation > 0f)
                m_orientation = Quaternion.RotateTowards(m_orientation, target.m_orientation, maxRotation);
        }

        // 0x06000b45: the original uses separate ordered Transform setters.
        public void ApplyTo(Transform t)
        {
            t.position = m_location;
            t.rotation = m_orientation;
        }
    }
}
