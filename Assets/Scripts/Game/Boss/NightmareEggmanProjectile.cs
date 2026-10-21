using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

namespace HardlightProject
{
    // Original Game.Runtime 0200066b and nested 0200066c; bodies translated from the 1.10.1 x86-64 binary,
    // 0x57D330..0x57EB40. The projectile flies a spline a random distance ahead of the player, holds, charges back along
    // the spline towards the player and explodes (cools down) shortly after reaching it. Its visual behaviours add the
    // authored offsets on top of the spline position.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class NightmareEggmanProjectile : Projectile
    {
        public enum ProjectileMovementState
        {
            ProgressingAheadOfPlayer = 0,
            HoldingPosition = 1,
            ChargingPlayer = 2,
            ReachedPlayer = 3
        }

        [SerializeField] private UnityEvent m_onReachedTargetDistance;
        [SerializeField] private UnityEvent m_onFiringAtPlayer;
        [SerializeField] private NightmareEggmanProjectileVisualBehaviour[] m_offsetVisualBehaviours;
        [SerializeField] private float m_metersAheadOfPlayerMin = 20f;
        [SerializeField] private float m_metersAheadOfPlayerMax = 25f;
        [SerializeField] private float m_timeToStayAheadOfPlayerMin = 2f;
        [SerializeField] private float m_timeToStayAheadOfPlayerMax = 4f;
        [SerializeField] private float m_returnSplineDistancePerSecond = 5f;
        [SerializeField] private AnimationCurve m_returnSplineDistancePerSecondCurve;
        [SerializeField, Tooltip("When the spline follower distance from player is below this we activate 'ReachedPlayer'")]
        private float m_splineMetersCloseToPlayer = 4f;
        [SerializeField] private float m_secondsUntilExplosionOnceClose = 2f;
        private Spline m_spline;
        private ISpline m_worldSpline;
        private Tracker2D m_tracker2D;
        private readonly Dictionary<ISurface, ISurface[]> m_trackableSurfaces = new Dictionary<ISurface, ISurface[]>();
        private readonly List<ISurface> m_trackableSurfacesList = new List<ISurface>();
        private Vector3 m_nearestSplineLocationWorld;
        private Vector3 m_lastFrameMovement;
        private float m_timeToStayAheadOfPlayer;
        private float m_timeInMotionPhase;
        private float m_desiredMetersAheadOfPlayer;
        private ProjectileMovementState m_currentMovementState;

        // 060022ab (1.10.1 RVA 0x57D330): picks the random distance and hold time, starts on the spline that distance
        // ahead of the player, and then fires like any projectile (the base Fire is inlined at the end).
        public override void Fire()
        {
            m_desiredMetersAheadOfPlayer = Random.Range(m_metersAheadOfPlayerMin, m_metersAheadOfPlayerMax);
            m_timeToStayAheadOfPlayer = Random.Range(m_timeToStayAheadOfPlayerMin, m_timeToStayAheadOfPlayerMax);
            m_currentMovementState = ProjectileMovementState.ProgressingAheadOfPlayer;
            for (int i = 0; i < m_offsetVisualBehaviours.Length; ++i)
                m_offsetVisualBehaviours[i].RegisterStateChange(m_currentMovementState);
            m_worldSpline = m_spline.GetSpline();
            Vector3 startPosition = m_worldSpline
                .GetWorldTransformFromDistance(PlayerSplineDistance() - m_desiredMetersAheadOfPlayer).Location;
            m_trackableSurfaces.Clear();
            m_trackableSurfacesList.Clear();
            m_trackableSurfaces.Add(m_worldSpline, Array.Empty<ISurface>());
            m_trackableSurfacesList.Add(m_worldSpline);
            if (m_tracker2D == null)
                m_tracker2D = new Tracker2D(new CollisionResolver2D());
            m_tracker2D.SetWorldInformation(m_trackableSurfacesList, m_trackableSurfaces);
            m_tracker2D.Teleport(startPosition);
            transform.position = startPosition;
            m_lastFrameMovement = transform.position;
            base.Fire();
        }

        // 060022ac (1.10.1 RVA 0x57D8E0): the player's distance along the spline.
        private float PlayerSplineDistance()
        {
            return m_worldSpline.FindNearestSplineLocation(m_characterManager.GetCurrentCharacterUnsafe().WorldPosition)
                .SplineDistance;
        }

        // 060022ad (1.10.1 RVA 0x57D9D0): without a world spline there is no motion. Each phase moves the projectile along
        // the spline, then checks whether the phase is over. The result is the step back from the new position.
        protected override Vector3 DoMovement(float adjustedDeltaTime)
        {
            if (m_worldSpline == null)
                return Vector3.zero;
            Vector3 motion = Vector3.zero;
            m_lastFrameMovement = transform.position;
            m_timeInMotionPhase += adjustedDeltaTime;
            switch (m_currentMovementState)
            {
                case ProjectileMovementState.ProgressingAheadOfPlayer:
                    MoveAheadOfPlayer(adjustedDeltaTime, out motion);
                    transform.position = motion;
                    CheckIfDestinationReached();
                    break;
                case ProjectileMovementState.HoldingPosition:
                    HoldRelativePosition(out motion);
                    transform.position = motion;
                    CheckAheadOfPlayerTimer();
                    break;
                case ProjectileMovementState.ChargingPlayer:
                    ReturnToPlayerSplinePosition(adjustedDeltaTime, out motion, true);
                    transform.position = motion;
                    CheckDistance();
                    break;
                case ProjectileMovementState.ReachedPlayer:
                    ReturnToPlayerSplinePosition(adjustedDeltaTime, out motion, false);
                    transform.position = motion;
                    CheckSecondsUntilCooldownEntry();
                    break;
            }
            for (int i = 0; i < m_offsetVisualBehaviours.Length; ++i)
                m_offsetVisualBehaviours[i].DoMovement(adjustedDeltaTime);
            return m_lastFrameMovement - transform.position;
        }

        // 060022ae (1.10.1 RVA 0x57E7A0): only the visual behaviours collide; the first one that hits supplies the hit.
        protected override bool DoCollisions(Vector3 _, out RaycastHit hitInfo)
        {
            for (int i = 0; i < m_offsetVisualBehaviours.Length; ++i)
            {
                if (m_offsetVisualBehaviours[i].DoCollisions(m_definition.CollisionLayerMask, out hitInfo))
                    return true;
            }
            hitInfo = default;
            return false;
        }

        // 060022af (1.10.1 RVA 0x57E3C0): the charge ends once the remaining distance is within the close-to-player range.
        private void CheckDistance()
        {
            if (m_desiredMetersAheadOfPlayer > m_splineMetersCloseToPlayer)
                return;
            ChangeState(ProjectileMovementState.ReachedPlayer);
        }

        // 060022b0 (1.10.1 RVA 0x57E440): once close, the projectile cools down (explodes) after the configured seconds.
        private void CheckSecondsUntilCooldownEntry()
        {
            if (m_secondsUntilExplosionOnceClose > m_timeInMotionPhase)
                return;
            EnterCooldown();
        }

        // 060022b1 (1.10.1 RVA 0x57DDF0): moves forward along the tracked spline at the definition's velocity.
        private void MoveAheadOfPlayer(float adjustedDeltaTime, out Vector3 motion)
        {
            float distance = adjustedDeltaTime * m_definition.Velocity;
            m_tracker2D.MoveLocal(Vector3.forward * distance);
            motion = m_tracker2D.Location.m_worldPosition;
            transform.rotation = m_tracker2D.Location.m_worldRotation;
        }

        // 060022b2 (1.10.1 RVA 0x57DF10): reaching the desired distance ahead of the player starts the hold.
        private void CheckIfDestinationReached()
        {
            float playerDistance = PlayerSplineDistance();
            float distance = m_worldSpline.FindNearestSplineLocation(transform.position).SplineDistance;
            if (distance > playerDistance + m_desiredMetersAheadOfPlayer)
            {
                ChangeState(ProjectileMovementState.HoldingPosition);
                m_onReachedTargetDistance.Invoke();
            }
        }

        // 060022b3 (1.10.1 RVA 0x57E090): stays the desired distance ahead of the player's spline position.
        private void HoldRelativePosition(out Vector3 motion)
        {
            LightweightTransform target = m_worldSpline
                .GetWorldTransformFromDistance(PlayerSplineDistance() + m_desiredMetersAheadOfPlayer);
            motion = target.Location;
            transform.rotation = target.Orientation;
        }

        // 060022b4 (1.10.1 RVA 0x57E1B0): after the hold time the projectile charges the player.
        private void CheckAheadOfPlayerTimer()
        {
            if (m_timeInMotionPhase <= m_timeToStayAheadOfPlayer)
                return;
            ChangeState(ProjectileMovementState.ChargingPlayer);
            m_onFiringAtPlayer.Invoke();
        }

        // 060022b5 (1.10.1 RVA 0x57E240): closes the distance to the player along the spline, with the authored curve
        // scaling the speed when charging and at full speed afterwards.
        private void ReturnToPlayerSplinePosition(float adjustedDeltaTime, out Vector3 motion, bool curveMovement)
        {
            float speedScale = curveMovement ? m_returnSplineDistancePerSecondCurve.Evaluate(m_timeInMotionPhase) : 1f;
            float closedMeters = speedScale * m_returnSplineDistancePerSecond * adjustedDeltaTime;
            m_desiredMetersAheadOfPlayer -= closedMeters;
            LightweightTransform target = m_worldSpline
                .GetWorldTransformFromDistance(PlayerSplineDistance() + m_desiredMetersAheadOfPlayer);
            motion = target.Location;
            transform.rotation = target.Orientation;
        }

        // 060022b6 (1.10.1 RVA 0x57E940)
        private void ChangeState(ProjectileMovementState newState)
        {
            m_currentMovementState = newState;
            m_timeInMotionPhase = 0f;
            for (int i = 0; i < m_offsetVisualBehaviours.Length; ++i)
                m_offsetVisualBehaviours[i].RegisterStateChange(m_currentMovementState);
        }

        // 060022b7 (1.10.1 RVA 0x57E9A0)
        public void SetSpline(Spline spline)
        {
            m_spline = spline;
        }

        // 060022b8 (1.10.1 RVA 0x57E9C0)
        protected override bool ShouldIgnoreCollider(Collider collider)
        {
            return false;
        }

        // 060022b9 (1.10.1 RVA 0x57E9D0): the projectile follows the spline, not a focus.
        public override void UpdateFocus(Vector3 target)
        {
        }

        // 060022ba (1.10.1 RVA 0x57E9E0)
        protected override bool ShouldTeleportToHitPoint()
        {
            return false;
        }
    }
}
