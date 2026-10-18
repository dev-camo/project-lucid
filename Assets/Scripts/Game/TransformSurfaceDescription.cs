using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 02000812 with nested FollowModeType 02000813 and SurfaceEvents 02000814;
    // bodies translated from the 1.10.1 x86-64 binary.
    // One segment of a follower's path: it knows its length, how long it takes to cross and how far
    // along it a target is at a given time. Subclasses supply the geometry.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Serializable]
    public abstract class TransformSurfaceDescription : TransformSurfaceProperty
    {
        public enum FollowModeType
        {
            Speed = 0,
            Time = 1,
            ProgressAnimated = 2,
            TimeSmoothed = 3,
            SpeedLerp = 4,
            SpeedAnimated = 5
        }

        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Serializable]
        public class SurfaceEvents
        {
            [SerializeField]
            private UnityEvent m_onBeginningReached = new UnityEvent();

            [SerializeField]
            private UnityEvent m_onEndReached = new UnityEvent();

            // 06002ee0 (0x5FF170)
            public void OnBeginningReached()
            {
                m_onBeginningReached.Invoke();
            }

            // 06002ee1 (0x5FF200)
            public void OnEndReached()
            {
                m_onEndReached.Invoke();
            }
        }

        private const float FloatPrecision = 0.01f;

        protected static readonly Quaternion m_inverseRotation = Quaternion.AngleAxis(180f, Vector3.up);

        [SerializeField]
        [Tooltip("How to control the follow speed along the surface.")]
        private FollowModeType m_followMode;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Follow surface by a set speed.")]
        private float m_followSpeed = 1f;

        [Min(0f)]
        [Tooltip("Follow surface takes this amount of time for the entire distance.")]
        [SerializeField]
        private float m_followTime = 1f;

        [SerializeField]
        [Tooltip("Follow surface by animated normalised distance.")]
        private AnimationCurve m_followAnim = new AnimationCurve();

        [SerializeField]
        [Tooltip("Surface events to fire.")]
        private SurfaceEvents m_events = new SurfaceEvents();

        [SerializeField]
        [Tooltip("Local position offset for this point.")]
        private Vector3 m_position;

        protected Vector3 m_positionWorld;
        protected Vector3 m_positionStartWorld;
        protected Vector3 m_positionEndWorld;
        protected Vector3 m_positionEndLocal;
        protected float m_distance;
        protected float m_timeTotal;
        private float m_speedStart;
        private float m_speedEnd;
        protected float m_extentsMultiplier = 1f;

        public Vector3 LocalPosition => m_position;

        public float Distance => m_distance;

        public float FollowTime => m_followTime;

        public float FollowSpeed => m_followSpeed;

        public FollowModeType FollowMode => m_followMode;

        public float TimeTotal => m_timeTotal;

        public Vector3 PositionWorld => m_positionWorld;

        public Vector3 PositionStartWorld => m_positionStartWorld;

        public Vector3 PositionEndWorld => m_positionEndWorld;

        public Vector3 PositionEndLocal => m_positionEndLocal;

        public float SpeedStart => m_speedStart;

        public float SpeedEnd => m_speedEnd;

        public virtual bool Orientate => true;

        // 06002ec8 (0x5FF150)
        public virtual void OnStart()
        {
            m_events.OnBeginningReached();
        }

        // 06002ec9 (0x5FF180): the point a target is placed at when it begins on this surface, at the
        // end when travelling backwards.
        public virtual Vector3 Teleport(int direction, bool usesTransform, Transform transform, Tracker2D tracker = null)
        {
            if (usesTransform)
                return transform.TransformPoint(direction > 0 ? m_position : m_positionEndLocal);
            return direction > 0 ? m_positionStartWorld : m_positionEndWorld;
        }

        // 06002eca (0x5FE480): a point surface; it is only ever traversed when it takes time.
        public virtual bool Update(out Vector3 position, ref float time, ref float deltaTime, int direction, bool usesTransform,
            Transform transform, Tracker2D tracker = null)
        {
            position = usesTransform ? transform.TransformPoint(m_position) : m_positionWorld;
            return m_timeTotal > 0f;
        }

        // 06002ecb (0x5FF1E0)
        public virtual void OnEnd()
        {
            m_events.OnEndReached();
        }

        public abstract Quaternion GetRotation(Transform transform, int direction, bool orientateToDirection, Tracker2D tracker = null);

        public abstract Quaternion GetRotationLocal(Transform transform);

        protected abstract void CalculateDistance();

        // 06002ecf (0x5FF210): how fast the surface starts and ends, by follow mode. SpeedLerp keeps
        // the values it was given by SetSpeedLimits.
        private void CalculateSpeedLimits()
        {
            switch (m_followMode)
            {
                case FollowModeType.Speed:
                    m_speedStart = m_followSpeed;
                    m_speedEnd = m_followSpeed;
                    break;
                case FollowModeType.Time:
                case FollowModeType.TimeSmoothed:
                    float speed = m_timeTotal > 0f ? m_distance / m_timeTotal : 0f;
                    m_speedStart = speed;
                    m_speedEnd = speed;
                    break;
                case FollowModeType.ProgressAnimated:
                    if (m_followAnim.length > 0)
                    {
                        float firstKeyTime = m_followAnim[0].time;
                        m_speedStart = firstKeyTime > 0f ? m_distance / firstKeyTime : 0f;
                    }
                    else
                    {
                        m_speedStart = 0f;
                    }
                    m_speedEnd = m_timeTotal > 0f ? m_distance / m_timeTotal : 0f;
                    break;
                case FollowModeType.SpeedAnimated:
                    m_speedStart = m_followAnim.Evaluate(0f);
                    m_speedEnd = m_followAnim.Evaluate(m_timeTotal);
                    break;
            }
        }

        // 06002ed0 (0x5FF330)
        public virtual void SetSpeedLimits(float speedStart, float speedEnd)
        {
            if (m_followMode != FollowModeType.SpeedLerp)
                return;
            m_speedStart = speedStart;
            m_speedEnd = speedEnd;
            CalculateTime();
        }

        // 06002ed1 (0x5FF350): time to cross the surface by follow mode; a speed of zero (or an empty
        // curve) takes no time.
        private void CalculateTime()
        {
            switch (m_followMode)
            {
                case FollowModeType.Speed:
                    m_timeTotal = m_followSpeed > 0f ? m_distance / m_followSpeed : 0f;
                    break;
                case FollowModeType.Time:
                case FollowModeType.TimeSmoothed:
                    m_timeTotal = m_followTime;
                    break;
                case FollowModeType.ProgressAnimated:
                case FollowModeType.SpeedAnimated:
                    m_timeTotal = m_followAnim.length > 0 ? m_followAnim[m_followAnim.length - 1].time : 0f;
                    break;
                case FollowModeType.SpeedLerp:
                    m_timeTotal = m_distance * 2f / (m_speedStart + m_speedEnd);
                    break;
            }
        }

        // 06002ed2 (0x5FF430)
        public float GetTimeByDistance(float distance)
        {
            if (distance >= m_distance)
                return m_timeTotal;
            return m_timeTotal * distance / m_distance;
        }

        // 06002ed3 (0x5FF450)
        public virtual float GetTimeByAngle(float angle)
        {
            return 0f;
        }

        // 06002ed4 (0x5FF460)
        public float GetDistanceByTime(float time, int direction)
        {
            return GetProgressByTime(time, direction) * m_distance;
        }

        // 06002ed5 (0x5FE7F0): fraction of the surface covered after the given time, counted from the
        // far end when the direction is negative.
        protected float GetProgressByTime(float time, int direction)
        {
            if (direction < 0)
                time = m_timeTotal - time;
            if (m_followMode == FollowModeType.ProgressAnimated && m_followAnim != null)
                return m_followAnim.Evaluate(time);
            if (m_followMode == FollowModeType.SpeedAnimated && m_followAnim != null)
                return m_followAnim.Area(0f, time, 0.1f) / m_followAnim.Area(0f, 0f, 0.1f);
            float progress = time / m_timeTotal;
            if (m_followMode == FollowModeType.TimeSmoothed)
                return Mathf.SmoothStep(0f, m_timeTotal, progress) / m_timeTotal;
            if (m_followMode == FollowModeType.SpeedLerp)
                return time * Mathf.Lerp(m_speedStart, m_speedEnd, progress * 0.5f) / m_distance;
            return progress;
        }

        // 06002ed6 (0x5FF490): places the surface at its own point and the next one in the follower's
        // space, then measures it. Only the end position is used here.
        public virtual void Initialise(TransformSurfaceFollower follower, Vector3 localPositionEnd, Vector3 localPositionPrev,
            Quaternion localRotationEnd)
        {
            Transform transform = follower.transform;
            SetPosition(transform.TransformPoint(m_position), transform);
            SetPositionEnd(transform.TransformPoint(localPositionEnd), transform);
            CalculateDistance();
            CalculateTime();
            CalculateSpeedLimits();
        }

        // 06002ed7 (0x5FF540)
        public virtual void OnValidate()
        {
            CalculateDistance();
            CalculateTime();
            m_position = m_position.ClampToInt(FloatPrecision);
            m_positionWorld = m_positionWorld.ClampToInt(FloatPrecision);
            m_positionStartWorld = m_positionStartWorld.ClampToInt(FloatPrecision);
            m_positionEndWorld = m_positionEndWorld.ClampToInt(FloatPrecision);
            m_positionEndLocal = m_positionEndLocal.ClampToInt(FloatPrecision);
            m_speedStart = MathUtilities.ClampToInt(m_speedStart, FloatPrecision);
            m_speedEnd = MathUtilities.ClampToInt(m_speedEnd, FloatPrecision);
        }

        // 06002ed8 (0x5FF650)
        public virtual void OnValidateTracker(Tracker2D tracker)
        {
        }

        // 06002ed9 (0x5FF660)
        public virtual Spline GetSpline()
        {
            return null;
        }

        // 06002eda (0x5FED60): true when the world position moved by more than the tolerance.
        public virtual bool SetPosition(Vector3 worldPosition, Transform transform)
        {
            bool unchanged = worldPosition.WithinTolerance(m_positionWorld, ValueTolerance);
            if (!unchanged)
            {
                m_positionWorld = worldPosition;
                m_position = transform.InverseTransformPoint(worldPosition);
            }
            return !unchanged;
        }

        public abstract void SetPositionEnd(Vector3 worldPosition, Transform transform);

        // 06002edc (0x5FF670)
        public void SetFollowMode(FollowModeType followMode, float followSpeed = 0f, float followTime = 0f)
        {
            m_followMode = followMode;
            m_followSpeed = followSpeed;
            m_followTime = followTime;
        }

        // 06002edd (0x5FEED0): scales the stored length and time by the change in multiplier, then
        // measures the surface again.
        public virtual void SetExtentsMultiplier(float extentsMultiplier)
        {
            float ratio = extentsMultiplier / m_extentsMultiplier;
            m_distance = ratio * m_distance;
            m_timeTotal = ratio * m_timeTotal;
            m_extentsMultiplier = extentsMultiplier;
            CalculateDistance();
            CalculateTime();
            CalculateSpeedLimits();
        }
    }
}
