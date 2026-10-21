using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 02000665 and nested 02000666/02000667; bodies translated from the 1.10.1 x86-64
    // binary, 0x57A930..0x57CB20. The local functions of Hit and ReturnProjectile keep the native
    // <Hit>g__..|39_N and <ReturnProjectile>g__..|48_N names, which fixes their declaration order.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class GuardianProjectile : Projectile
    {
        public enum ProjectileMovementState
        {
            FiredAsArc = 0,
            ReturningAsArc = 1,
            FiredStraight = 2,
            ReturningStraight = 3,
            FiredOverhead = 4,
            ReturningFromOverhead = 5,
            MoveToResting = 6,
            Resting = 7,
            ExplodingInPlace = 8
        }

        [SerializeField] private UnityEvent m_onHitBossPhase4;
        [SerializeField] private float m_lerpMovementTime;
        [SerializeField] private EaseUtilities.EaseType m_lerpEasing;
        [SerializeField] private UnityEvent m_onArriveAtRestingPlace;
        [SerializeField] private UnityEvent m_onBossReadyForFinalHit;
        private Vector3 m_centerOfCircle = Vector3.zero;
        private float m_radiusOfCircle;
        private float m_progressionArcRadians;
        private float m_travelSpeedRadians;
        private float m_cullTimeOnceBehindPlayer;
        private float m_returningRadians;
        private float m_finalRadius;
        private float m_cachedY;
        private Vector3 m_targetDirection;
        private float m_startingDistanceSq;
        private float m_currentDistanceSq;
        private ProjectileMovementState m_movementState;
        private BossManager m_bossManager;
        private GuardianManager m_guardianManager;
        private Transform m_returnTransform;
        private Vector3 m_startLerpMovementPosition;
        private Transform m_finalRestingTransform;
        private bool m_movementIsClockwise;
        private Boss m_boss;
        private bool m_behindPlayerSoPreparingToDie;
        private bool m_hasHitBoss;
        private Vector3 m_straightVelocity;
        private Vector3 m_previousFramePosition;
        private Spline m_spline;
        private ISpline m_worldSpline;
        private Tracker2D m_tracker2D;
        private readonly Dictionary<ISurface, ISurface[]> m_trackableSurfaces = new Dictionary<ISurface, ISurface[]>();
        private readonly List<ISurface> m_trackableSurfacesList = new List<ISurface>();
        private Vector3 m_nearestSplineLocationWorld;
        private float m_distanceAlongSpline;
        private float m_lerpTime;

        // 0x57A930: aims straight at the target at the definition's velocity.
        public override void UpdateFocus(Vector3 target)
        {
            Transform self = transform;
            Vector3 direction = (target - self.position).normalized;
            m_straightVelocity = direction * m_definition.Velocity;
            self.rotation = Quaternion.LookRotation(direction);
        }

        // 0x57AA70
        protected override Vector3 DoMovement(float adjustedDeltaTime)
        {
            switch (m_movementState)
            {
                case ProjectileMovementState.FiredAsArc:
                    return ArcMovement(adjustedDeltaTime, false);
                case ProjectileMovementState.ReturningAsArc:
                    return ArcMovement(adjustedDeltaTime, true);
                case ProjectileMovementState.FiredStraight:
                    return StraightLineMovement(adjustedDeltaTime, false);
                case ProjectileMovementState.ReturningStraight:
                    return StraightLineMovement(adjustedDeltaTime, true);
                case ProjectileMovementState.FiredOverhead:
                    return SplineMovement(adjustedDeltaTime);
                case ProjectileMovementState.ReturningFromOverhead:
                    // Keeps steering at the return point while it moves.
                    UpdateFocus(m_returnTransform.position);
                    return StraightLineMovement(adjustedDeltaTime, true);
                case ProjectileMovementState.MoveToResting:
                    return LerpMovement(adjustedDeltaTime);
                default:
                    return Vector3.zero;
            }
        }

        // 0x57B4A0: what a hit does depends on the movement state. The local functions are
        // 0x57B7D0, 0x57B9B0, 0x57B820 and 0x57B610 in the order of their numbers.
        protected override void Hit(Collider hitCollider)
        {
            switch (m_movementState)
            {
                case ProjectileMovementState.FiredAsArc:
                case ProjectileMovementState.FiredStraight:
                    HitIfNotHomingAttack();
                    break;
                case ProjectileMovementState.ReturningAsArc:
                case ProjectileMovementState.ReturningStraight:
                    HitBossAndStagger();
                    break;
                case ProjectileMovementState.FiredOverhead:
                    HitIfNotHomingAttack();
                    CheckIfTrackAndExplode();
                    break;
                case ProjectileMovementState.ReturningFromOverhead:
                    HitBossDealDamageAndBounceOff();
                    break;
            }

            void HitBossAndStagger()
            {
                if (m_bossManager.IsColliderBoss(hitCollider, out Boss boss))
                {
                    boss.Stagger();
                    EnterCooldown();
                }
            }

            void CheckIfTrackAndExplode()
            {
                if (hitCollider.gameObject.layer == LayerMask.NameToLayer("Track"))
                {
                    if (ProjectileType == ProjectileType.GuardianKillerStraightProjectile)
                        EnterCooldown();
                    m_movementState = ProjectileMovementState.ExplodingInPlace;
                }
            }

            void HitBossDealDamageAndBounceOff()
            {
                if (!m_bossManager.IsColliderBoss(hitCollider, out Boss boss) || m_hasHitBoss) return;
                m_hasHitBoss = true;
                if (!m_guardianManager.TryGetFreeReturnPoint(out Transform restingPoint))
                {
                    Kill();
                    return;
                }
                m_guardianManager.FinalHitReady += InvokeFinalHitReadiness;
                m_finalRestingTransform = restingPoint;
                m_startLerpMovementPosition = m_returnTransform.position;
                m_movementState = ProjectileMovementState.MoveToResting;
                m_lifeRemainingSeconds = float.PositiveInfinity;
                boss.ReceiveHit();
                m_onHitBossPhase4.Invoke();
            }

            // The player is hurt unless a homing attack (or, for the returnable projectiles, a
            // ground homing attack or boost) is in use.
            void HitIfNotHomingAttack()
            {
                if (!IsColliderPlayer(hitCollider, out Character character)) return;
                if (ActorAbilityUtilities.IsAbilityInUse(character, ActorAbilityType.Character_HomingAttackAirTarget)) return;
                if (ProjectileType == ProjectileType.GuardianReturnableProjectile
                    || ProjectileType == ProjectileType.GuardianReturnableStraightProjectile)
                {
                    if (ActorAbilityUtilities.IsAbilityInUse(character, ActorAbilityType.Character_HomingAttackGroundRunning)) return;
                    if (ActorAbilityUtilities.IsAbilityInUse(character, ActorAbilityType.Character_HomingAttackGroundRolling)) return;
                    if (ActorAbilityUtilities.IsAbilityInUse(character, ActorAbilityType.Character_MovementGroundBoost)) return;
                }
                character.TryTakeDamage(m_hazardDefinition, OnProcessDamageAction);
                EnterCooldown();
            }
        }

        // 0x57BA40: every damaging action reports the hit to the boss; ReturnDamage is not expected here.
        private void OnProcessDamageAction(DamageAction damageAction, Collider damagedCollider)
        {
            switch (damageAction)
            {
                case DamageAction.None:
                    return;
                case DamageAction.HardFailure:
                case DamageAction.Stumble:
                case DamageAction.Knockback:
                    m_boss.RegisterHitPlayer(damageAction, m_hazardDefinition.Type);
                    return;
                default:
                    throw new ArgumentOutOfRangeException("damageAction", damageAction, null);
            }
        }

        // 0x57BB10: native evaluates the player-collider test while the projectile is fired but its
        // result never reaches the return value; only the returning states ignore the player.
        protected override bool ShouldIgnoreCollider(Collider collider)
        {
            switch (m_movementState)
            {
                case ProjectileMovementState.FiredAsArc:
                case ProjectileMovementState.FiredStraight:
                    IsColliderPlayer(collider, out _);
                    return false;
                case ProjectileMovementState.ReturningAsArc:
                case ProjectileMovementState.ReturningStraight:
                    return IsColliderPlayer(collider, out _);
                default:
                    return false;
            }
        }

        // 0x57ACA0: outbound the projectile circles the arc centre at its own height; returning it
        // reverses direction and spirals radius and height in towards the return point.
        private Vector3 ArcMovement(float adjustedDeltaTime, bool returning)
        {
            Vector3 newPosition;
            if (returning)
            {
                m_currentDistanceSq = m_returnTransform.InverseTransformPoint(transform.position).sqrMagnitude;
                float returnStep = adjustedDeltaTime * m_returningRadians;
                m_progressionArcRadians += m_movementIsClockwise ? returnStep : -returnStep;
                m_finalRadius = m_returnTransform.position.magnitude;
                float progress = (float)(m_startingDistanceSq - m_currentDistanceSq) / m_startingDistanceSq;
                float radius = Mathf.Lerp(m_radiusOfCircle, m_finalRadius, progress);
                float height = Mathf.Lerp(m_cachedY, m_returnTransform.position.y, progress);
                newPosition = m_centerOfCircle.PointOnCircleWithFixedYLHS(m_progressionArcRadians, radius, height);
            }
            else
            {
                float travelStep = adjustedDeltaTime * m_travelSpeedRadians;
                m_progressionArcRadians += m_movementIsClockwise ? -travelStep : travelStep;
                newPosition = m_centerOfCircle.PointOnCircleWithFixedYLHS(m_progressionArcRadians, m_radiusOfCircle, transform.position.y);
            }
            m_previousFramePosition = transform.position;
            transform.position = newPosition;
            Vector3 movement = newPosition - m_previousFramePosition;
            // A non-negative or unordered dot product (ucomiss, ordered less-than skip) marks the projectile
            // as behind the player and starts the cull countdown.
            if (!returning && m_characterManager.TryGetCurrentCharacter(out Character character) && !m_behindPlayerSoPreparingToDie)
            {
                float dot = Vector3.Dot(transform.position - character.WorldPosition, movement);
                if (!(dot < 0f))
                {
                    m_behindPlayerSoPreparingToDie = true;
                    m_lifeRemainingSeconds = m_cullTimeOnceBehindPlayer;
                }
            }
            return movement;
        }

        // 0x57BD00: reads the movement state and direction the boss FSM published for this projectile.
        public override void Fire()
        {
            base.Fire();
            m_bossManager = ProcessManager.GetSystem<BossManager>();
            m_guardianManager = ProcessManager.GetSystem<GuardianManager>();
            m_movementState = m_boss.Storage.GetValue(BossFSMKeys.GuardianProjectileMovement, ProjectileMovementState.FiredAsArc, true);
            m_progressionArcRadians = Mathf.Atan2(transform.position.x, transform.position.z);
            m_centerOfCircle = new Vector3(0f, transform.position.y, 0f);
            m_radiusOfCircle = Vector3.Distance(transform.position, m_centerOfCircle);
            m_movementIsClockwise = m_boss.Storage.GetValue(BossFSMKeys.GuardianIsClockwise, false, true);
            m_behindPlayerSoPreparingToDie = false;
        }

        // 0x57C100
        public void SetArcCenter(Vector3 center)
        {
            m_centerOfCircle = center;
        }

        // 0x57C120
        public void SetTargetDirection(Vector3 direction)
        {
            m_straightVelocity = direction * m_definition.Velocity;
            transform.rotation = Quaternion.LookRotation(direction);
        }

        // 0x57C180
        public void SetProgressionRates(float rate, float returnRate)
        {
            m_travelSpeedRadians = rate;
            m_returningRadians = returnRate;
        }

        // 0x57C1A0
        public void SetCullingValues(float cullTimeOnceBehindPlayer)
        {
            m_cullTimeOnceBehindPlayer = cullTimeOnceBehindPlayer;
        }

        // 0x57C1B0: turns a fired projectile around towards the boss's return point and extends its life.
        public void ReturnProjectile()
        {
            switch (m_movementState)
            {
                case ProjectileMovementState.FiredAsArc:
                    ReturnArc();
                    break;
                case ProjectileMovementState.FiredStraight:
                    ReturnStraight();
                    m_movementState = ProjectileMovementState.ReturningStraight;
                    break;
                case ProjectileMovementState.FiredOverhead:
                case ProjectileMovementState.ExplodingInPlace:
                    ReturnStraight();
                    m_movementState = ProjectileMovementState.ReturningFromOverhead;
                    break;
            }
            m_lifeRemainingSeconds += m_definition.MaxLifespanSeconds;

            void ReturnArc()
            {
                m_returnTransform = m_boss.GetSpawnPoint<Projectile>(ActorAttachPointType.BossGuardian_ProjectileReturnPoint);
                Vector3 position = transform.position;
                Vector3 offset = m_returnTransform.position - position;
                m_startingDistanceSq = offset.sqrMagnitude;
                m_currentDistanceSq = m_startingDistanceSq;
                m_cachedY = position.y;
                m_movementState = ProjectileMovementState.ReturningAsArc;
            }

            void ReturnStraight()
            {
                m_returnTransform = m_boss.GetSpawnPoint<Collider>(ActorAttachPointType.BossGuardian_ProjectileSpawnPoint_Phase4);
                UpdateFocus(m_returnTransform.position);
            }
        }

        // 0x57B080: the translation is applied in world space before the behind-player test.
        private Vector3 StraightLineMovement(float adjustedDeltaTime, bool returning)
        {
            Vector3 movement = m_straightVelocity * adjustedDeltaTime;
            transform.Translate(movement, Space.World);
            // Same behind-player test as ArcMovement.
            if (!returning && m_characterManager.TryGetCurrentCharacter(out Character character) && !m_behindPlayerSoPreparingToDie)
            {
                float dot = Vector3.Dot(transform.position - character.WorldPosition, movement);
                if (!(dot < 0f))
                {
                    m_behindPlayerSoPreparingToDie = true;
                    m_lifeRemainingSeconds = m_cullTimeOnceBehindPlayer;
                }
            }
            return movement;
        }

        // 0x57B160: eases from the position it started at to the resting transform, then rests.
        private Vector3 LerpMovement(float adjustedDeltaTime)
        {
            m_lerpTime += adjustedDeltaTime;
            float easedProgress = EaseUtilities.Ease(m_lerpEasing, m_lerpTime / m_lerpMovementTime);
            Vector3 currentPosition = transform.position;
            Vector3 startPosition = m_startLerpMovementPosition;
            Vector3 restingPosition = m_finalRestingTransform.position;
            if (easedProgress > 1f)
            {
                m_movementState = ProjectileMovementState.Resting;
                m_onArriveAtRestingPlace?.Invoke();
                return Vector3.zero;
            }
            Vector3 newPosition = Vector3.Lerp(startPosition, restingPosition, easedProgress);
            transform.position = newPosition;
            return newPosition - currentPosition;
        }

        // 0x57B2B0: moves along the world spline at the definition's velocity, rotation before position.
        private Vector3 SplineMovement(float adjustedDeltaTime)
        {
            LightweightTransform previous = m_worldSpline.GetWorldTransformFromDistance(m_distanceAlongSpline);
            m_distanceAlongSpline += (float)(adjustedDeltaTime * m_definition.Velocity);
            LightweightTransform current = m_worldSpline.GetWorldTransformFromDistance(m_distanceAlongSpline);
            transform.rotation = current.Orientation;
            transform.position = current.Location;
            return current.Location - previous.Location;
        }

        // 0x57C520
        protected override bool ShouldTeleportToHitPoint()
        {
            return false;
        }

        // 0x57C530
        public void SetBoss(Boss boss)
        {
            m_boss = boss;
        }

        // 0x57C550: the projectile follows the spline's world form and tracks it with a 2D tracker.
        public void SetSpline(Spline spline)
        {
            m_spline = spline;
            m_worldSpline = m_spline.GetSpline();
            m_trackableSurfaces.Clear();
            m_trackableSurfacesList.Clear();
            m_trackableSurfaces.Add(m_worldSpline, Array.Empty<ISurface>());
            m_trackableSurfacesList.Add(m_worldSpline);
            if (m_tracker2D == null)
                m_tracker2D = new Tracker2D(new CollisionResolver2D());
            m_tracker2D.SetWorldInformation(m_trackableSurfacesList, m_trackableSurfaces);
            m_distanceAlongSpline = 0f;
            m_hasHitBoss = false;
        }

        // 0x57C810: a projectile that is returning or resting is left alone when exceptions are allowed.
        public override void ForceEnterCoolDown(bool allowExceptions)
        {
            if (allowExceptions)
            {
                switch (m_movementState)
                {
                    case ProjectileMovementState.ReturningAsArc:
                    case ProjectileMovementState.ReturningFromOverhead:
                    case ProjectileMovementState.MoveToResting:
                    case ProjectileMovementState.Resting:
                        return;
                }
            }
            base.ForceEnterCoolDown(allowExceptions);
        }

        // 0x57C880
        private void InvokeFinalHitReadiness()
        {
            m_onBossReadyForFinalHit?.Invoke();
        }

        // 0x57C8A0: without a guardian manager the base teardown is skipped as well.
        public override void OnDestroy()
        {
            if (m_guardianManager == null) return;
            m_guardianManager.FinalHitReady -= InvokeFinalHitReadiness;
            base.OnDestroy();
        }
    }
}
