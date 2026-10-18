using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000819 with nested WrapModeType 0200081a; bodies translated from the
    // 1.10.1 x86-64 binary.
    // Drives its targets along a chain of surfaces, each target tracking its own surface index,
    // time and direction.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class TransformSurfaceFollower : TimeScaledComponent_SDT
    {
        public enum WrapModeType
        {
            Once = 0,
            Loop = 1,
            PingPong = 2
        }

        [Tooltip("If the transform is not moved at runtime, set static as true for better performance.")]
        [SerializeField]
        private bool m_static = true;

        [SerializeField]
        [Tooltip("Logic when at end of all surfaces.")]
        private WrapModeType m_wrapMode = WrapModeType.PingPong;

        [Tooltip("A closed circuit will return back to the initial position before completing.")]
        [SerializeField]
        private bool m_closed;

        [Tooltip("All targets that will follow surfaces.")]
        [SerializeField]
        private List<TransformSurfaceTarget> m_targets = new List<TransformSurfaceTarget>();

        [Tooltip("All surfaces that compound together for the duration of being moved.")]
        [SerializeReference]
        private List<TransformSurfaceDescription> m_surfaces = new List<TransformSurfaceDescription>();

        public bool IsRuntime { get; private set; } = true;

        public float TimeMultiplier { get; private set; } = 1f;

        public Action<TransformSurfaceFollower> OnCompleteAction;

        public bool Static => m_static;

        public WrapModeType WrapMode => m_wrapMode;

        public bool Closed => m_closed;

        public int NumSurfaces => m_surfaces.Count;

        // 06002f0f (0x601480)
        protected override void Awake()
        {
            base.Awake();
            InternalAwake();
            InternalReset();
        }

        // 06002f10 (0x601B90)
        public void SetEnabled(bool enable)
        {
            enabled = enable;
        }

        // 06002f11 (0x601BA0)
        protected override void OnDisable()
        {
            base.OnDisable();
            OnCompleteAction?.Invoke(this);
        }

        // 06002f12 (0x6014B0)
        private void InternalAwake()
        {
            foreach (TransformSurfaceTarget target in m_targets)
                target.OnAwake(this);
        }

        // 06002f13 (0x6015E0): hands each surface the point it ends at and the point before it, so the
        // last surface leads into the first when closed and otherwise ends where it starts. Speed limits
        // then run the speed out of one surface into the next, continuing across the join when closed.
        private void InternalReset()
        {
            bool closed = m_closed;
            TransformSurfaceDescription previous = m_surfaces[m_surfaces.Count - 1];
            Vector3 positionPrev = previous.LocalPosition;
            bool first = true;
            foreach (TransformSurfaceDescription surface in m_surfaces)
            {
                TransformSurfaceDescription end = first && !closed ? previous : surface;
                first = false;
                previous.Initialise(this, end.LocalPosition, positionPrev, end.GetRotationLocal(transform));
                positionPrev = previous.LocalPosition;
                previous = surface;
            }

            int count = m_surfaces.Count;
            float speedEndPrev = m_closed ? m_surfaces[count - 1].SpeedEnd : 0f;
            if (count > 0)
            {
                float speedStartNext = count == 1 ? speedEndPrev : m_surfaces[1].SpeedStart;
                TransformSurfaceDescription firstSurface = m_surfaces[0];
                firstSurface.SetSpeedLimits(speedEndPrev, speedStartNext);
                float speedStartFirst = m_closed ? firstSurface.SpeedStart : 0f;
                for (int i = 1; i < count; i++)
                {
                    float speedStart = m_surfaces[i - 1].SpeedEnd;
                    float speedEnd = i < count - 1 ? m_surfaces[i + 1].SpeedStart : speedStartFirst;
                    m_surfaces[i].SetSpeedLimits(speedStart, speedEnd);
                }
            }

            foreach (TransformSurfaceTarget target in m_targets)
                target.Initialise(this);
            TimeMultiplier = 1f;
        }

        // 06002f14 (0x6020D0)
        protected override void InternalUpdate(float deltaTime)
        {
            foreach (TransformSurfaceTarget target in m_targets)
                target.Update(this, TimeMultiplier * deltaTime);
        }

        // 06002f15 (0x602630)
        public void ResetTargets()
        {
            InternalReset();
            foreach (TransformSurfaceTarget target in m_targets)
                target.Reset(this);
            InternalUpdate(0f);
        }

        // 06002f16 (0x602860)
        public void SnapTargetsToNearest()
        {
            SnapTargetsToNearest(null);
        }

        // 06002f17 (0x602870)
        public void SnapTargetsToNearest(Transform targetTransform)
        {
            InternalReset();
            foreach (TransformSurfaceTarget target in m_targets)
                target.SnapToNearest(this, targetTransform);
            InternalUpdate(0f);
        }

        // 06002f18 (0x602BE0)
        public void Play(float speedMultiplier)
        {
            if (gameObject.activeInHierarchy)
            {
                enabled = true;
                TimeMultiplier = Mathf.Abs(speedMultiplier);
            }
        }

        // 06002f19 (0x602C30): a negative multiplier plays every target backwards.
        public void PlayWithDirection(float speedMultiplier)
        {
            if (!gameObject.activeInHierarchy)
                return;
            Play(speedMultiplier);
            int direction = speedMultiplier < 0f ? -1 : 1;
            foreach (TransformSurfaceTarget target in m_targets)
            {
                target.SetDirection(direction);
                target.Play();
            }
        }

        // 06002f1a (0x602E30): updates the targets once, by a whole number of frames when the multiplier
        // is below one in size.
        public void AdvanceInDirection(float speedMultiplier)
        {
            float frames = speedMultiplier == 0f ? 1f : Mathf.CeilToInt(1f / Mathf.Abs(speedMultiplier));
            float deltaTime = GetDeltaTime() * frames;
            foreach (TransformSurfaceTarget target in m_targets)
                target.Update(this, deltaTime);
        }

        // 06002f1b (0x603010)
        public void SetExtentsMultiplier(float extentsMultiplier)
        {
            foreach (TransformSurfaceDescription surface in m_surfaces)
                surface.SetExtentsMultiplier(extentsMultiplier);
        }

        // 06002f1c (0x603140)
        public void Stop()
        {
            if (!gameObject.activeInHierarchy)
                return;
            foreach (TransformSurfaceTarget target in m_targets)
                target.Stop();
        }

        // 06002f1d (0x6033A0)
        public void AddSurface(TransformSurfaceDescription surface)
        {
            m_surfaces.Add(surface);
        }

        // 06002f1e (0x601EA0)
        public TransformSurfaceDescription GetSurface(int surfaceIndex)
        {
            if (surfaceIndex >= 0 && surfaceIndex < m_surfaces.Count)
                return m_surfaces[surfaceIndex];
            return null;
        }

        // 06002f1f (0x603430): the first spline any surface offers.
        public Spline GetSurfaceSpline()
        {
            foreach (TransformSurfaceDescription surface in m_surfaces)
            {
                Spline spline = surface?.GetSpline();
                if (spline != null)
                    return spline;
            }
            return null;
        }

        // 06002f20 (0x6035E0)
        protected override void OnValidate()
        {
            base.OnValidate();
            if (!gameObject.CanValidate())
                return;
            foreach (TransformSurfaceDescription surface in m_surfaces)
                surface?.OnValidate();
            foreach (TransformSurfaceTarget target in m_targets)
                target.OnValidate();
        }

        // 06002f21 (0x603830): walks the surfaces taking off each one's length until the distance falls
        // inside one. The direction is positive while the distance left is below the total of all
        // surfaces, which compares the remainder in the final surface to the whole chain.
        public void GetSurfaceByDistance(float distance, out int surfaceIndex, out float time, out int direction)
        {
            surfaceIndex = 0;
            time = 0f;
            direction = 1;
            if (distance == 0f)
                return;
            TransformSurfaceDescription surface = m_surfaces[surfaceIndex];
            while (distance >= surface.Distance)
            {
                int index = surfaceIndex;
                distance -= surface.Distance;
                if (index == m_surfaces.Count - 1)
                    break;
                surfaceIndex++;
                surface = m_surfaces[surfaceIndex];
            }
            time = surface.GetTimeByDistance(distance);
            direction = GetTotalDistance() > distance ? 1 : -1;
        }

        // 06002f22 (0x603B00)
        public void GetSurfaceByAngle(float angle, out int surfaceIndex, out float time, out int direction)
        {
            surfaceIndex = 0;
            time = m_surfaces[0].GetTimeByAngle(angle);
            direction = 1;
        }

        // 06002f23 (0x603B90)
        public void GetSurfaceByNormalisedValue(float normalisedValue, out int surfaceIndex, out float time, out int direction)
        {
            GetSurfaceByDistance(GetTotalDistance() * normalisedValue, out surfaceIndex, out time, out direction);
        }

        // 06002f24 (0x6039B0)
        private float GetTotalDistance()
        {
            float total = 0f;
            foreach (TransformSurfaceDescription surface in m_surfaces)
                total += surface != null ? surface.Distance : 0f;
            return total;
        }
    }
}
