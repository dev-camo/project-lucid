using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;

namespace HardlightProject
{
    // Original Game.Runtime 02000a2e; bodies translated from the 1.10.1 x86-64 binary.
    // Places this object at a target or at the surface point nearest to it, takes the surface
    // rotation, and can flip that rotation when it faces too far from the target's forward.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class SurfaceObjectFollower : TimeScaledComponent_SDT
    {
        private enum PositionTarget
        {
            TargetTransform = 0,
            SurfacePosition = 1
        }

        [SerializeField]
        private Transform m_targetTransform;

        [FormerlySerializedAs("m_splineTargetOffset")]
        [Tooltip("Added to target position before finding nearest position on surface.")]
        [SerializeField]
        private Vector3 m_targetOffset;

        [SerializeField]
        private PositionTarget m_positionTarget;

        [Tooltip("Whether the rotation can be inverted if angle exceeds the tolerance.")]
        [SerializeField]
        private bool m_canInvertRotationAngle = true;

        [Range(0f, 180f)]
        [Tooltip("Inverse the rotation when the angle between the target forward and this forward exceeds this value.")]
        [ShowIf("m_canInvertRotationAngle", (string)null)]
        [SerializeField]
        private float m_inverseRotationAngle = 95f;

        private Transform m_currentTargetTransform;
        private ISurface m_surface;
        private bool m_rotationIsReversed;
        private bool m_hasTarget;
        private readonly Quaternion m_inverseRotation = Quaternion.AngleAxis(180f, Vector3.up);
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>();

        protected abstract ISurface GetSurface();

        protected override void Awake()
        {
            base.Awake();
            m_surface = GetSurface();
            m_hasTarget = m_targetTransform != null;
            m_currentTargetTransform = m_targetTransform;
            m_levelManagerRef.InvokeOnValid(OnLevelManagerValid);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (m_levelManagerRef.TryGet(out LevelManager levelManager))
                levelManager.OnLevelRestart -= ResetTarget;
        }

        private void OnLevelManagerValid(LevelManager levelManager)
        {
            levelManager.OnLevelRestart += ResetTarget;
        }

        private void ResetTarget()
        {
            m_currentTargetTransform = m_targetTransform;
            m_hasTarget = m_currentTargetTransform != null;
            UpdateTransform(true);
        }

        public void SetTarget(Transform target)
        {
            m_currentTargetTransform = target;
            m_hasTarget = m_currentTargetTransform != null;
            UpdateTransform(true);
        }

        protected override void InternalUpdate(float deltaTime)
        {
            UpdateTransform(m_canInvertRotationAngle);
        }

        // 06003a5c (0x6B63A0): the flip decision is taken after the rotation for this update has
        // been chosen, so a newly reversed rotation is first applied on the following update.
        private void UpdateTransform(bool canInvertRotationAngle)
        {
            if (!m_hasTarget)
                return;
            SurfaceLocation surfaceLocation = GetSurfaceLocation(m_surface);
            Quaternion rotation = surfaceLocation.m_worldRotation;
            switch (m_positionTarget)
            {
                case PositionTarget.TargetTransform:
                    transform.position = m_currentTargetTransform.position;
                    break;
                case PositionTarget.SurfacePosition:
                    transform.position = surfaceLocation.m_worldPosition;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            if (m_rotationIsReversed)
                rotation *= m_inverseRotation;
            if (canInvertRotationAngle && Vector3.Angle(rotation * Vector3.forward, m_currentTargetTransform.forward) > m_inverseRotationAngle)
                m_rotationIsReversed = !m_rotationIsReversed;
            transform.rotation = rotation;
        }

        private SurfaceLocation GetSurfaceLocation(ISurface surface)
        {
            return surface.FindNearestSurfaceLocationFromWorld(m_currentTargetTransform.position + m_targetOffset);
        }

        private void OnDrawGizmosSelected()
        {
            if (m_currentTargetTransform == null)
                return;
            m_surface = GetSurface();
            if (m_surface == null)
                return;
            SurfaceLocation surfaceLocation = GetSurfaceLocation(m_surface);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(surfaceLocation.m_worldPosition, 1f);
            Quaternion rotation = surfaceLocation.m_worldRotation;
            if (m_rotationIsReversed)
                rotation *= m_inverseRotation;
            Gizmos.color = Color.white;
            Gizmos.DrawRay(surfaceLocation.m_worldPosition, rotation * Vector3.forward);
        }
    }
}
