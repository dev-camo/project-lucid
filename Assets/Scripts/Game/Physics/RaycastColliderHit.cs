using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Complete original02000766 source candidate. Genuine ColliderTrackingUtilities
    // remains an unclosed dependency; this class is private and unaccepted.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class RaycastColliderHit
    {
        private bool m_valid;
        private bool m_hit;
        private RaycastHit m_hitInfo;
        private float m_hitDistance;
        private bool m_hitIsEdge;
        private float m_minDistance;
        private float m_maxDistance;

        // Original06002975 publishes minimum before maximum.
        public void SetDistances(float minDistance, float maxDistance)
        {
            m_minDistance = minDistance;
            m_maxDistance = maxDistance;
        }

        // Original06002976 copies all seven fields, including thresholds.
        public void Set(RaycastColliderHit other)
        {
            m_valid = other.m_valid;
            m_hit = other.m_hit;
            m_hitInfo = other.m_hitInfo;
            m_hitDistance = other.m_hitDistance;
            m_hitIsEdge = other.m_hitIsEdge;
            m_minDistance = other.m_minDistance;
            m_maxDistance = other.m_maxDistance;
        }

        public bool Valid => m_valid; //06002977
        public bool Hit => m_hit && m_hitDistance < m_minDistance; //06002978
        public bool HitProjected => m_hit && m_hitDistance < m_maxDistance; //06002979
        public bool HitIsEdge => m_hit && m_hitIsEdge; //0600297a: raw hit guard.
        public Transform HitTransform => m_hitInfo.transform; //0600297b: no hit guard.

        // Original0600297c uses the Ray overload and preserves stale distance/edge
        // on a miss or projected-only hit. Valid is published before the engine call.
        public void Raycast(Vector3 origin, Vector3 direction, float offset, Vector3 worldUp, int layerMask)
        {
            bool isCeiling = false;
            var ray = new Ray(origin, direction);
            float maxDistance = m_maxDistance + offset;
            m_valid = true;
            m_hit = Physics.Raycast(ray, out m_hitInfo, maxDistance, layerMask);
            if (m_hit)
            {
                m_hitDistance = m_hitInfo.distance - offset;
                if (Hit)
                    m_hitIsEdge = ColliderTrackingUtilities.ContactIsEdge(m_hitInfo.normal, worldUp, out isCeiling);
            }
        }

        // Original0600297d retains info, distance, and both thresholds.
        public void Invalidate()
        {
            m_valid = false;
            m_hit = false;
            m_hitIsEdge = false;
        }

        public float HitDistance() => m_hitDistance; //0600297e
        public Vector3 HitPosition() => m_hitInfo.point; //0600297f
        public Vector3 HitNormal() => m_hitInfo.normal; //06002980
        public T GetComponent<T>() => m_hitInfo.collider.GetComponent<T>(); //06002981, unconstrained.
        public RaycastColliderHit() { } //06002982: genuine base-only constructor.
    }
}
