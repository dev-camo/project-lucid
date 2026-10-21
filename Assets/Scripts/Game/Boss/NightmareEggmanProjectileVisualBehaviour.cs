using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HardlightProject
{
    // Original Game.Runtime 0200066d and nested 0200066e/0200066f; bodies translated from the 1.10.1 x86-64 binary,
    // 0x57D770 and 0x57E470..0x57F1E0. One visual part of the Nightmare Eggman projectile: for each movement phase of
    // the projectile it moves on top of the projectile by an authored (randomised, optionally curved) offset, homes in
    // on the player, or keeps its last direction, and optionally turns while doing so.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class NightmareEggmanProjectileVisualBehaviour : SmoothedTransformProxy
    {
        [Serializable]
        private class MovementPhaseSettings
        {
            public NightmareEggmanProjectile.ProjectileMovementState MovementState;
            [HideIf("HomeInOnPlayerPosition", (string)null)] public Vector3 OffsetMax;
            [HideIf("HomeInOnPlayerPosition", (string)null)] public Vector3 OffsetMin;
            public bool CurveX;
            [ShowIf("CurveX", (string)null)] public AnimationCurve AnimationCurveX;
            [ShowIf("CurveX", (string)null)] public float CurveXTime = 1f;
            public bool CurveY;
            [ShowIf("CurveY", (string)null)] public AnimationCurve AnimationCurveY;
            [ShowIf("CurveY", (string)null)] public float CurveYTime = 1f;
            public bool CurveZ;
            [ShowIf("CurveZ", (string)null)] public AnimationCurve AnimationCurveZ;
            [ShowIf("CurveZ", (string)null)] public float CurveZTime = 1f;
            [Tooltip("Curves will act as a lerp % from initial missile offset to an average of the last X frames of player position")]
            public bool HomeInOnPlayerPosition;
            [ShowIf("HomeInOnPlayerPosition", (string)null)] public float LerpXYTime = 10f;
            [Tooltip("Base offset will carry over from previous state.")]
            public bool MaintainPreviousBaseOffset;
            [Tooltip("Curved offset will carry over from previous state and applied as base offset, use to stop a curve and maintain position.")]
            public bool MaintainPreviousCurvedOffsetAsNewBaseOffset;
            public bool RotateDuringPhase;
            [ShowIf("RotateDuringPhase", (string)null)] public RotationType RotationType;
            [HideInInspector] public Vector3 Offset;

            // 060022c7 (1.10.1 RVA 0x57ECB0): picks the offset at random between the minimum and maximum, per axis.
            public void CalculateOffset()
            {
                float x = Random.Range(OffsetMin.x, OffsetMax.x);
                float y = Random.Range(OffsetMin.y, OffsetMax.y);
                float z = Random.Range(OffsetMin.z, OffsetMax.z);
                Offset = new Vector3(x, y, z);
            }

            // 060022c8 (1.10.1 RVA 0x57F1E0)
            public MovementPhaseSettings()
            {
            }
        }

        private enum RotationType
        {
            RotateToPlayer = 0,
            RotateToWorldMovementDirection = 1,
            RotateToLocalMovementDirection = 2
        }

        [SerializeField] private MovementPhaseSettings[] m_stateSettings;
        [SerializeField] private NightmareEggmanProjectileVisualBehaviour[] m_offsetVisualBehaviours;
        [SerializeField] private Collider m_collider;
        private CharacterManager m_characterManager;
        private float m_timeToStayAheadOfPlayer;
        private float m_timeAheadOfPlayer;
        private float m_desiredMetersAheadOfPlayer;
        private float m_timeInMotionPhase;
        private Vector3 m_lastFrameLocalPosition;
        private Vector3 m_lastFrameWorldPosition;
        private Vector3 m_movementDirection;
        private NightmareEggmanProjectile.ProjectileMovementState m_currentMovementState;
        private MovementPhaseSettings m_currentMovementSettings;
        private readonly RaycastHit[] m_raycastHits = new RaycastHit[4];

        // 060022bc (1.10.1 RVA 0x57EB40): finds the character manager, rolls every phase's offset and then initialises
        // the transform rotator.
        protected override void Awake()
        {
            ProcessManager.GetSystemRef<CharacterManager>(null, true).InvokeOnValid(SetCharacterManager);
            for (int i = 0; i < m_stateSettings.Length; ++i)
                m_stateSettings[i].CalculateOffset();
            base.Awake();
        }

        // 060022bd (1.10.1 RVA 0x57ED20)
        private void SetCharacterManager(CharacterManager manager)
        {
            m_characterManager = manager;
        }

        // 060022be (1.10.1 RVA 0x57E470): moves on top of the projectile according to the phase, remembers the step taken
        // as the movement direction, and moves the nested visual behaviours.
        public void DoMovement(float deltaTime)
        {
            m_timeInMotionPhase += deltaTime;
            Vector3 offsetMotion = Vector3.zero;
            m_lastFrameLocalPosition = transform.localPosition;
            switch (m_currentMovementState)
            {
                case NightmareEggmanProjectile.ProjectileMovementState.ProgressingAheadOfPlayer:
                case NightmareEggmanProjectile.ProjectileMovementState.HoldingPosition:
                    DoOffsetMovement(out offsetMotion);
                    transform.localPosition = offsetMotion;
                    HandleRotationForPhase(deltaTime);
                    break;
                case NightmareEggmanProjectile.ProjectileMovementState.ChargingPlayer:
                    if (m_currentMovementSettings.HomeInOnPlayerPosition)
                        MoveToPlayerXandY(out offsetMotion);
                    else
                        DoOffsetMovement(out offsetMotion);
                    transform.localPosition = offsetMotion;
                    HandleRotationForPhase(deltaTime);
                    break;
                case NightmareEggmanProjectile.ProjectileMovementState.ReachedPlayer:
                    LockedTrajectoryMovement(out offsetMotion);
                    transform.localPosition = offsetMotion;
                    break;
            }
            m_movementDirection = offsetMotion - m_lastFrameLocalPosition;
            for (int i = 0; i < m_offsetVisualBehaviours.Length; ++i)
                m_offsetVisualBehaviours[i].DoMovement(deltaTime);
        }

        // 060022bf (1.10.1 RVA 0x57ED40)
        private void DoOffsetMovement(out Vector3 offsetMotion)
        {
            offsetMotion = CurveOffset(m_currentMovementSettings.Offset);
        }

        // 060022c0 (1.10.1 RVA 0x57E820): with nested behaviours only they collide (and this one never reports a hit);
        // otherwise the collider is swept from where it was last frame and the last hit supplies the hit.
        public bool DoCollisions(LayerMask collisionLayerMask, out RaycastHit hitInfo)
        {
            hitInfo = default;
            if (m_offsetVisualBehaviours.Length != 0)
            {
                for (int i = 0; i < m_offsetVisualBehaviours.Length; ++i)
                {
                    if (m_offsetVisualBehaviours[i].DoCollisions(collisionLayerMask, out hitInfo))
                        return true;
                }
                return false;
            }
            Vector3 movement = transform.position - m_lastFrameWorldPosition;
            int count = m_collider.PhysicsCastFromCollider(movement, collisionLayerMask, m_raycastHits,
                QueryTriggerInteraction.Ignore);
            if (count > 0)
                hitInfo = m_raycastHits[count - 1];
            m_lastFrameWorldPosition = transform.position;
            return count != 0;
        }

        // 060022c1 (1.10.1 RVA 0x57EF70): blends from the authored offset to the player's position (in the parent's
        // space) in x and y, and to zero in z, over LerpXYTime.
        private void MoveToPlayerXandY(out Vector3 offsetMotion)
        {
            Character character = m_characterManager.GetCurrentCharacterUnsafe();
            Vector3 startOffset = m_currentMovementSettings.Offset;
            Vector3 playerLocal = transform.parent.transform.InverseTransformPoint(character.WorldPosition);
            float blend = m_timeInMotionPhase / m_currentMovementSettings.LerpXYTime;
            offsetMotion = Vector3.Lerp(startOffset, new Vector3(playerLocal.x, playerLocal.y, 0f), blend);
        }

        // 060022c2 (1.10.1 RVA 0x57EE20): turns towards the player or along the movement direction of the phase. An
        // unknown rotation type looks along zero.
        private void HandleRotationForPhase(float deltaTime)
        {
            if (!m_currentMovementSettings.RotateDuringPhase)
                return;
            Vector3 direction;
            switch (m_currentMovementSettings.RotationType)
            {
                case RotationType.RotateToPlayer:
                    direction = m_characterManager.GetCurrentCharacterUnsafe().WorldPosition - transform.position;
                    break;
                case RotationType.RotateToWorldMovementDirection:
                    direction = m_movementDirection;
                    break;
                case RotationType.RotateToLocalMovementDirection:
                    direction = transform.localPosition - m_lastFrameLocalPosition;
                    break;
                default:
                    direction = Vector3.zero;
                    break;
            }
            UpdateRotation(Quaternion.LookRotation(direction, Vector3.up), deltaTime);
        }

        // 060022c3 (1.10.1 RVA 0x57F040): keeps moving by the last step.
        private void LockedTrajectoryMovement(out Vector3 offsetMotion)
        {
            Transform self = transform;
            offsetMotion = self.localPosition + m_movementDirection;
            self.localPosition = offsetMotion;
        }

        // 060022c4 (1.10.1 RVA 0x57F0B0): scales each axis by its authored curve at the phase time.
        private Vector3 CurveOffset(Vector3 offset)
        {
            if (m_currentMovementSettings.CurveX)
                offset.x *= m_currentMovementSettings.AnimationCurveX.Evaluate(m_timeInMotionPhase / m_currentMovementSettings.CurveXTime);
            if (m_currentMovementSettings.CurveY)
                offset.y *= m_currentMovementSettings.AnimationCurveY.Evaluate(m_timeInMotionPhase / m_currentMovementSettings.CurveYTime);
            if (m_currentMovementSettings.CurveZ)
                offset.z *= m_currentMovementSettings.AnimationCurveZ.Evaluate(m_timeInMotionPhase / m_currentMovementSettings.CurveZTime);
            return offset;
        }

        // 060022c5 (1.10.1 RVA 0x57D770): switches to the settings of the new state, which may inherit the previous
        // offset, restarts the phase clock and passes the change on.
        public void RegisterStateChange(NightmareEggmanProjectile.ProjectileMovementState newMovementState)
        {
            m_currentMovementState = newMovementState;
            for (int i = 0; i < m_stateSettings.Length; ++i)
            {
                MovementPhaseSettings settings = m_stateSettings[i];
                if (settings.MovementState != newMovementState)
                    continue;
                if (settings.MaintainPreviousBaseOffset)
                    settings.Offset = m_currentMovementSettings.Offset;
                if (settings.MaintainPreviousCurvedOffsetAsNewBaseOffset)
                {
                    settings.Offset = m_currentMovementSettings.Offset;
                    // The native code still evaluates the previous phase's curves here but keeps the uncurved offset.
                    CurveOffset(m_currentMovementSettings.Offset);
                }
                m_currentMovementSettings = settings;
            }
            m_timeInMotionPhase = 0f;
            for (int i = 0; i < m_offsetVisualBehaviours.Length; ++i)
                m_offsetVisualBehaviours[i].RegisterStateChange(m_currentMovementState);
        }

        // 060022c6 (1.10.1 RVA 0x57F180)
        public NightmareEggmanProjectileVisualBehaviour()
        {
        }
    }
}
