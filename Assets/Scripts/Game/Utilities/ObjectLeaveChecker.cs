using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a14: reports once a tracked object has stayed
    // beyond a distance, from this transform or a collider's bounds, for a time.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ObjectLeaveChecker : TimeScaledComponent_SDT
    {
        [SerializeField] private float m_leaveDistanceThreshold = 5f;
        [SerializeField] private float m_minimumTime = 0.5f;
        [SerializeField]
        [Header("Can be null. Assign to use a bounds check rather than this transform's position.")]
        private Collider m_boundsCollider;
        public bool WaitingForObjectToLeave { get; private set; }
        private float m_leaveDistanceThresholdSqr;
        private float m_leaveTimer;
        private Action m_onLeaveCallback;
        private Transform m_objectToTrack;
        private bool m_usingColliderBoundsCheck;
        private Bounds m_colliderBoundsCache;

        // 060039bd: the bounds mode and the cached bounds are decided once here.
        private void Start()
        {
            UpdateCachedValues();
            m_usingColliderBoundsCheck = m_boundsCollider != null;
            if (m_usingColliderBoundsCheck)
                m_colliderBoundsCache = m_boundsCollider.bounds;
        }

        public void Activate(Transform objectToTrack, Action onLeaveCallback = null)
        {
            WaitingForObjectToLeave = true;
            m_leaveTimer = 0f;
            m_objectToTrack = objectToTrack;
            m_onLeaveCallback = onLeaveCallback;
        }

        // 060039bf: the timer still advances by this frame's delta after the
        // callback, so it restarts from deltaTime rather than zero.
        protected override void InternalUpdate(float deltaTime)
        {
            if (!WaitingForObjectToLeave || !HasObjectLeft())
            {
                m_leaveTimer = 0f;
                return;
            }
            if (m_leaveTimer >= m_minimumTime)
            {
                WaitingForObjectToLeave = false;
                m_leaveTimer = 0f;
                m_onLeaveCallback?.Invoke();
            }
            m_leaveTimer += deltaTime;
        }

        // 060039c0: a missing object counts as gone. The bounds check refreshes only
        // the cached centre, keeping the extents from Start.
        private bool HasObjectLeft()
        {
            if (m_objectToTrack == null)
                return true;
            Vector3 objectPosition = m_objectToTrack.position;
            float sqrDistance;
            if (m_usingColliderBoundsCheck)
            {
                m_colliderBoundsCache.center = m_boundsCollider.bounds.center;
                sqrDistance = m_colliderBoundsCache.SqrDistance(m_objectToTrack.position);
            }
            else
                sqrDistance = (objectPosition - transform.position).sqrMagnitude;
            return sqrDistance >= m_leaveDistanceThresholdSqr;
        }

        private void OnDrawGizmosSelected()
        {
            if (!enabled)
                return;
            if (m_boundsCollider != null)
                Gizmos.DrawWireCube(m_boundsCollider.bounds.center, m_boundsCollider.bounds.size);
            else
                Gizmos.DrawWireSphere(transform.position, m_leaveDistanceThreshold);
        }

        // 060039c2 includes the genuine discarded GameObject property access.
        protected override void OnValidate()
        {
            base.OnValidate();
            _ = gameObject;
            UpdateCachedValues();
        }

        private void UpdateCachedValues()
        {
            m_leaveDistanceThresholdSqr = m_leaveDistanceThreshold * m_leaveDistanceThreshold;
        }
    }
}
