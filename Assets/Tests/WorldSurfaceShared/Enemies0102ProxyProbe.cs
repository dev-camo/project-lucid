using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Tests.WorldSurfaceShared
{
    // Test-only visual proxy for the enemy ability fixtures. Edit Mode sends no Awake, so the transform cache is
    // filled the way SmoothedTransformRotator.Initialise fills it, and the rotation an ability asks for is recorded
    // instead of being written to the transform.
    public sealed class Enemies0102ProxyProbe : SmoothedTransformProxy
    {
        public int RotationWrites;
        public Quaternion LastRotation;

        protected override void Awake()
        {
        }

        public void CacheTransform()
        {
            m_cachedTransform = transform;
            m_hasCachedTransform = true;
        }

        protected override void SetRotation(Quaternion rotation)
        {
            RotationWrites++;
            LastRotation = rotation;
        }
    }
}
