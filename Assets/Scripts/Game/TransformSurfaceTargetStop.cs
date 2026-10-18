using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 0200081e with nested StopModeType 0200081f; bodies translated
    // from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class TransformSurfaceTargetStop
    {
        public enum StopModeType
        {
            EaseToPaused = 0,
            EaseToNext = 1,
            EaseToLast = 2,
            EaseToNearest = 3
        }

        [SerializeField]
        [Tooltip("When stop is activated, this defines the ease to stationary logic.")]
        private StopModeType m_stopModeType;

        [Tooltip("If current surface's progress exceeds threshold then apply stop logic from start of next surface.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float m_stopThreshold = 1f;

        [Tooltip("Defines the ease to stop curve.")]
        [SerializeField]
        private EaseUtilities.EaseType m_stopEaseType = EaseUtilities.EaseType.OutQuad;

        [Tooltip("Time to ease to stop.")]
        [Min(0f)]
        [SerializeField]
        private float m_stopEaseTime = 1f;

        [Tooltip("Whether end events are also fired on stop reached.")]
        [SerializeField]
        private bool m_fireEndEvents = true;

        [Tooltip("Events fired on stop completion.")]
        [SerializeField]
        private UnityEvent m_onStopReached = new UnityEvent();

        private TransformSurfaceTarget m_target;
        private bool m_stopRequested;
        private bool m_stopActive;
        private float m_stopTime;
        private float m_stopSurfaceTime;

        public bool FireEndEvents => m_fireEndEvents;

        // 06002f3b (0x603FA0)
        public void Initialise(TransformSurfaceTarget target)
        {
            m_stopRequested = false;
            m_stopActive = false;
            m_target = target;
        }

        // 06002f3c (0x604020)
        public void Reset()
        {
            m_stopRequested = false;
            m_stopActive = false;
        }

        // 06002f3d (0x6043C0): past the threshold of the current surface the request waits for the
        // next one. Otherwise the target may be turned back first (EaseToLast always, EaseToNearest
        // when in the first half of the surface), and the stop starts easing.
        public void Activate(float time, int direction)
        {
            TransformSurfaceDescription surface = m_target.Surface;
            float timeTotal = surface.TimeTotal;
            if (!m_stopActive && time > m_stopThreshold * timeTotal)
            {
                m_stopRequested = true;
                return;
            }
            if (m_stopModeType == StopModeType.EaseToLast || (m_stopModeType == StopModeType.EaseToNearest && 0.5f * timeTotal > time))
                m_target.SetDirection(-direction);
            m_stopRequested = false;
            m_stopActive = true;
            m_stopTime = 0f;
            m_stopSurfaceTime = timeTotal - time;
        }

        // 06002f3e (0x6044D0): eases the frame's delta time down while the stop is active. True
        // once the stop is complete or, for the surface-relative modes, for as long as it eases.
        public bool Update(ref float deltaTime, float timeMultiplier, float time, int direction)
        {
            if (m_stopRequested)
                Activate(time, direction);
            if (!m_stopActive)
                return false;

            float easeStart = 1f - m_stopTime / m_stopEaseTime;
            float timeStep = deltaTime / timeMultiplier;
            if (m_stopModeType == StopModeType.EaseToPaused)
            {
                m_stopTime += timeStep;
                if (m_stopEaseTime > m_stopTime)
                {
                    deltaTime = EaseUtilities.Ease(m_stopEaseType, easeStart) * deltaTime;
                    return false;
                }
                deltaTime = 0f;
                m_stopRequested = false;
                m_stopActive = false;
                m_onStopReached.Invoke();
                return true;
            }

            float remaining = (m_target.Surface.TimeTotal - time) / m_stopSurfaceTime;
            deltaTime = Mathf.Max(deltaTime * EaseUtilities.Ease(m_stopEaseType, easeStart * remaining), 0.001f);
            float surfaceRatio = m_stopSurfaceTime / m_stopEaseTime;
            deltaTime = Mathf.Max(deltaTime * surfaceRatio, timeStep * Mathf.Min(surfaceRatio * remaining, 1f));
            m_stopTime = deltaTime / timeMultiplier + m_stopTime;
            return true;
        }

        // 06002f3f (0x6044B0)
        public void OnStopReached()
        {
            if (!m_stopActive)
                return;
            m_stopRequested = false;
            m_stopActive = false;
            m_onStopReached.Invoke();
        }
    }
}
