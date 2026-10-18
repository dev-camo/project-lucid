using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;

namespace HardlightProject
{
    // Original Game.Runtime 0200081c with nested StartModeType 0200081d; bodies translated from the
    // 1.10.1 x86-64 binary.
    // One transform driven by a follower across its surfaces. It keeps its own surface, time along
    // that surface and direction, and handles wrapping at either end of the chain.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class TransformSurfaceTarget : TransformSurfaceProperty
    {
        public enum StartModeType
        {
            Distance = 0,
            Index = 1,
            Angle = 2,
            Normalised = 3
        }

        [Tooltip("The transform to be driven by a follower across multiple surfaces.")]
        [SerializeField]
        private Transform m_transform;

        [SerializeField]
        [Tooltip("Initial offset from surface.")]
        private Vector3 m_offset;

        [Tooltip("If checked, orientates target to the surface normal.")]
        [SerializeField]
        private bool m_orientate;

        [Tooltip("If orientate checked, orientates to current forward direction.")]
        [SerializeField]
        private bool m_orientateToDirection = true;

        [SerializeField]
        [Tooltip("If orientate checked, applies an offset rotation when travelling forward.")]
        [FormerlySerializedAs("m_rotationLocal")]
        private Vector3 m_rotationLocalForward;

        [Tooltip("If orientate checked, applies an offset rotation when travelling backward.")]
        [SerializeField]
        private Vector3 m_rotationLocalBackward;

        [Tooltip("Starting direction, if true will start in opposite direction of surface movement.")]
        [SerializeField]
        private bool m_startDirectionReversed;

        [Tooltip("Starting position logic.")]
        [SerializeField]
        private StartModeType m_startModeType;

        [SerializeField]
        [Tooltip("Starting position determined by distance along surfaces.")]
        [Min(0f)]
        private float m_startDistance;

        [Min(0f)]
        [SerializeField]
        [Tooltip("Starting position determined by surface index.")]
        private int m_startIndex;

        [Tooltip("Starting position determined by angle on surface.")]
        [SerializeField]
        [Min(0f)]
        private int m_startAngle;

        [SerializeField]
        [Tooltip("Starting position determined by normalised value of the total distance.")]
        [Range(0f, 1f)]
        private float m_normalisedValue;

        [Tooltip("Surface events to fire.")]
        [SerializeField]
        private TransformSurfaceDescription.SurfaceEvents m_events = new TransformSurfaceDescription.SurfaceEvents();

        [SerializeField]
        [Tooltip("Logic to bring the target to a stop when triggered.")]
        private TransformSurfaceTargetStop m_stop = new TransformSurfaceTargetStop();

        private int m_surfaceIndex;
        private TransformSurfaceDescription m_surface;
        private float m_time;
        private int m_direction;
        private Vector3 m_worldOffset;
        private Quaternion m_rotationWorldToLocalForward;
        private Quaternion m_rotationWorldToLocalBackward;
        private bool m_ignoreEndEvents;
        private Tracker2D m_tracker;
        private CharacterCollisionData[] m_collisionData;

        public TransformSurfaceDescription Surface => m_surface;

        // 06002f28 (0x603CD0): picks the surface, time and direction the target starts from. A reversed
        // start flips the direction once the surface is known.
        private void GetInitialState(TransformSurfaceFollower follower)
        {
            switch (m_startModeType)
            {
                case StartModeType.Distance:
                    follower.GetSurfaceByDistance(m_startDistance, out m_surfaceIndex, out m_time, out m_direction);
                    break;
                case StartModeType.Index:
                    m_surfaceIndex = m_startIndex;
                    m_time = 0f;
                    m_direction = 1;
                    break;
                case StartModeType.Angle:
                    follower.GetSurfaceByAngle(m_startAngle, out m_surfaceIndex, out m_time, out m_direction);
                    break;
                case StartModeType.Normalised:
                    follower.GetSurfaceByNormalisedValue(m_normalisedValue, out m_surfaceIndex, out m_time, out m_direction);
                    break;
                default:
                    m_surfaceIndex = 0;
                    m_time = 0f;
                    m_direction = 1;
                    break;
            }
            m_surface = follower.GetSurface(m_surfaceIndex);
            if (m_startDirectionReversed)
                SetDirection(-1);
        }

        // 06002f29 (0x601BE0): the offset rotations for each direction, the collision data below the
        // transform, and a tracker over the follower's spline when it has one.
        public void OnAwake(TransformSurfaceFollower follower)
        {
            if (m_transform == null)
                return;
            m_rotationWorldToLocalForward = Quaternion.Euler(m_rotationLocalForward);
            m_rotationWorldToLocalBackward = Quaternion.Euler(m_rotationLocalBackward);
            SetCollisionData();
            Spline spline = follower.GetSurfaceSpline();
            if (spline == null)
                return;
            ISpline splineSurface = spline.GetSpline();
            List<ISurface> surfaces = new List<ISurface>();
            surfaces.Add(splineSurface);
            Dictionary<ISurface, ISurface[]> world = new Dictionary<ISurface, ISurface[]>();
            world.Add(splineSurface, new ISurface[0]);
            m_tracker = new Tracker2D(new CollisionResolver2D());
            m_tracker.SetWorldInformation(surfaces, world);
        }

        // 06002f2a (0x601F00)
        public void Initialise(TransformSurfaceFollower follower)
        {
            if (m_transform == null)
                return;
            m_worldOffset = follower.transform.rotation * m_offset;
            GetInitialState(follower);
            m_stop.Initialise(this);
            if (m_surface == null || m_tracker == null)
                return;
            ISpline spline = follower.GetSurfaceSpline().GetSpline();
            float distance = m_surface.GetDistanceByTime(m_time, m_direction);
            m_tracker.Teleport(spline.GetWorldTransformFromDistance(distance).Location);
            m_surface.OnStart();
        }

        // 06002f2b (0x603EC0)
        private void SetCollisionData()
        {
            m_collisionData = m_transform.GetComponentsInChildren<CharacterCollisionData>(true);
            for (int i = 0; i < m_collisionData.Length; i++)
                m_collisionData[i].SetMovement(true, Vector3.zero);
        }

        // 06002f2c (0x603FC0)
        private void UpdateCollisionData(Vector3 velocity)
        {
            if (m_collisionData == null)
                return;
            for (int i = 0; i < m_collisionData.Length; i++)
                m_collisionData[i].SetMovement(true, velocity);
        }

        // 06002f2d (0x602780)
        public void Reset(TransformSurfaceFollower follower)
        {
            m_stop.Reset();
            GetInitialState(follower);
            bool usesTransform = RefreshTransform(follower, out Transform transform);
            Vector3 position = m_surface.Teleport(m_direction, usesTransform, transform, m_tracker);
            SetTransform(follower, position, 0f);
            m_surface.OnStart();
        }

        // 06002f2e (0x604030): a static follower has no transform to follow; otherwise the offset is
        // turned by the follower's current rotation.
        private bool RefreshTransform(TransformSurfaceFollower follower, out Transform transform)
        {
            bool isStatic = follower.Static;
            if (isStatic)
            {
                transform = null;
            }
            else
            {
                transform = follower.transform;
                m_worldOffset = transform.rotation * m_offset;
            }
            return !isStatic;
        }

        // 06002f2f (0x6029D0): steps along the current surface in unscaled frame time until it reports it
        // is finished, and keeps the time of closest approach to the given transform (the follower's own when none is given).
        public void SnapToNearest(TransformSurfaceFollower follower, Transform targetTransform)
        {
            Transform source = targetTransform != null ? targetTransform : follower.transform;
            Vector3 targetPosition = source.position;
            bool usesTransform = RefreshTransform(follower, out Transform transform);
            float nearestTime = m_time;
            float nearestSqrDistance = float.MaxValue;
            while (true)
            {
                float deltaTime = Time.unscaledDeltaTime;
                if (!m_surface.Update(out Vector3 position, ref m_time, ref deltaTime, m_direction, usesTransform, transform, m_tracker))
                    break;
                position += m_worldOffset;
                float sqrDistance = (position - targetPosition).sqrMagnitude;
                if (sqrDistance < nearestSqrDistance)
                {
                    nearestTime = m_time;
                    nearestSqrDistance = sqrDistance;
                }
            }
            m_time = nearestTime;
        }

        // 06002f30 (0x602E20)
        public void Play()
        {
            m_stop.Reset();
        }

        // 06002f31 (0x602DF0): turning round mirrors the time along the surface.
        public void SetDirection(int direction)
        {
            if (m_direction == direction)
                return;
            m_direction = direction;
            m_time = m_surface.TimeTotal - m_time;
            m_ignoreEndEvents = true;
        }

        // 06002f32 (0x603310)
        public void Stop()
        {
            m_stop.Activate(m_time, m_direction);
        }

        // 06002f33 (0x604450)
        private void FireEndEvents(bool stopReached)
        {
            if (m_ignoreEndEvents)
                return;
            if (stopReached)
            {
                bool fireEndEvents = m_stop.FireEndEvents;
                m_stop.OnStopReached();
                if (!fireEndEvents)
                    return;
            }
            m_surface.OnEnd();
        }

        // 06002f34 (0x602210): moves along the current surface, carrying any time left over onto the next
        // surface or turning round at the ends of the chain according to the follower's wrap mode.
        public void Update(TransformSurfaceFollower follower, float deltaTime)
        {
            Vector3 position;
            if (m_surface == null)
                return;
            float remainingDeltaTime = deltaTime;
            bool usesTransform = RefreshTransform(follower, out Transform transform);
            bool stopReached = m_stop.Update(ref remainingDeltaTime, follower.TimeMultiplier, m_time, m_direction);
            while (!m_surface.Update(out position, ref m_time, ref remainingDeltaTime, m_direction, usesTransform, transform, m_tracker))
            {
                m_time = 0f;
                SetTransform(follower, position, deltaTime);
                FireEndEvents(stopReached);
                if (m_direction == 1 ? SetNextSurface(follower) : SetPrevSurface(follower))
                {
                    m_surface = follower.GetSurface(m_surfaceIndex);
                    m_surface.Update(out Vector3 _, ref m_time, ref m_time, m_direction, usesTransform, transform, m_tracker);
                }
                else if (follower.WrapMode == TransformSurfaceFollower.WrapModeType.PingPong)
                {
                    m_direction = -m_direction;
                }
                else if (follower.WrapMode == TransformSurfaceFollower.WrapModeType.Once)
                {
                    m_surfaceIndex = -1;
                    m_surface = null;
                    break;
                }
                position = m_surface.Teleport(m_direction, usesTransform, transform, m_tracker);
                m_surface.OnStart();
                if ((!follower.enabled || stopReached) && follower.IsRuntime)
                    break;
            }
            m_ignoreEndEvents = false;
            SetTransform(follower, position, deltaTime);
        }

        // 06002f35 (0x6040B0): places the transform at the surface position plus the offset, oriented to
        // the surface when both it and this target ask for it, and tells the collision data how fast it moved.
        private void SetTransform(TransformSurfaceFollower follower, Vector3 position, float deltaTime)
        {
            Vector3 worldPosition = position + m_worldOffset;
            if (deltaTime < ValueTolerance || m_surface == null)
                UpdateCollisionData(Vector3.zero);
            else
                UpdateCollisionData((worldPosition - m_transform.position) / deltaTime);
            if (m_transform == null)
                return;
            if (m_surface != null && m_surface.Orientate && m_orientate)
            {
                Quaternion rotation = m_surface.GetRotation(follower.transform, m_direction, m_orientateToDirection, m_tracker);
                rotation = rotation * (m_direction <= 0 ? m_rotationWorldToLocalBackward : m_rotationWorldToLocalForward);
                m_transform.SetPositionAndRotation(worldPosition, rotation);
            }
            else
            {
                m_transform.position = worldPosition;
            }
        }

        // 06002f36 (0x603820)
        public void OnValidate()
        {
        }

        // 06002f37 (0x604710): false once past the last surface unless the follower loops.
        private bool SetNextSurface(TransformSurfaceFollower follower)
        {
            if (m_surfaceIndex < follower.NumSurfaces - 1)
            {
                m_surfaceIndex++;
                return true;
            }
            m_events.OnEndReached();
            if (follower.WrapMode == TransformSurfaceFollower.WrapModeType.Loop)
            {
                m_surfaceIndex = 0;
                m_events.OnBeginningReached();
                return true;
            }
            return false;
        }

        // 06002f38 (0x6046A0)
        private bool SetPrevSurface(TransformSurfaceFollower follower)
        {
            if (m_surfaceIndex > 0)
            {
                m_surfaceIndex--;
                return true;
            }
            m_events.OnBeginningReached();
            if (follower.WrapMode == TransformSurfaceFollower.WrapModeType.Loop)
            {
                m_surfaceIndex = follower.NumSurfaces - 1;
                return true;
            }
            return false;
        }
    }
}
