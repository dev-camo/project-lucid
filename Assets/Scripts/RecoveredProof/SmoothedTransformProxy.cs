using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SmoothedTransformProxy : SmoothedTransformRotator
    {
        [SerializeField] private SameSpaceObjectAvoidance m_sameSpaceObjectAvoidance;
        [SerializeField] protected Transform m_animationRoot;
        private bool m_usesSameSpaceObjectAvoidance;
        // Original Game.Runtime 06003a22 / ARM64 68f318.
        public Transform AnimationRoot => m_animationRoot;
        // 06003a23 / 68f320 reparents this component, not the animation root.
        public void Detach(Transform newParent = null)
        {
            transform.parent = newParent;
            m_usesSameSpaceObjectAvoidance = m_sameSpaceObjectAvoidance != null;
        }
        // 06003a24 / 68f3b0.
        public void UpdatePositionAndRotation(Vector3 targetPosition, Quaternion targetRotation, float deltaTime)
        {
            if (IsDestroyed) return;
            UpdatePosition(targetPosition);
            UpdateRotation(targetRotation, deltaTime);
        }
        // 06003a25 / 68f5f4 retains the virtual overload and separate rotation gate.
        public virtual void UpdatePositionAndRotation(Vector3 targetPosition, Quaternion targetUpRotation,
            float forwardAngle, float deltaTime)
        {
            UpdatePosition(targetPosition);
            UpdateRotation(targetUpRotation, forwardAngle, deltaTime);
        }
        // 06003a26 / 68f8d8 deliberately bypasses the ordinary rotation override gate.
        public virtual void SnapToLocation(Vector3 targetPosition, Quaternion targetRotation)
        { UpdatePosition(targetPosition); SetRotation(targetRotation); }
        // 06003a27 / 68f4c0 preserves the cached receiver before offset lookup.
        private void UpdatePosition(Vector3 targetPosition)
        {
            if (IsDestroyed || !m_hasCachedTransform) return;
            m_cachedTransform.position = targetPosition;
            if (m_usesSameSpaceObjectAvoidance)
            {
                Transform cached = m_cachedTransform;
                Vector3 position = cached.position;
                cached.position = position + m_sameSpaceObjectAvoidance.Offset;
            }
        }
        // 06003a28 / 68f988 has no cached-transform presence flag check.
        public void SetScale(Vector3 scale) { if (!IsDestroyed) m_cachedTransform.localScale = scale; }
        // 06003a29 / 68f9a0.
        public void ToggleSSOA(bool active) { m_usesSameSpaceObjectAvoidance = active; }
        // 06003a2a / 68f9a8 is a genuine base-only constructor.
        public SmoothedTransformProxy() { }
    }
}
