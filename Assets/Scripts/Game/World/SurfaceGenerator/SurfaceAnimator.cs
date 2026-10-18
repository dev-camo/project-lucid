using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a9d; bodies translated from the 1.10.1 x86-64 binary.
    // Member order fixes the OnEnable lambda name <OnEnable>b__9_0.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SurfaceAnimator : MonoBehaviour, ITimeScaled
    {
        [SerializeField]
        [Tooltip("Defines the knots that will be animated.")]
        private List<SurfaceAnimatedKnot> m_knots = new List<SurfaceAnimatedKnot>();

        [Tooltip("Time category for animation to be applied in.")]
        [SerializeField]
        private TimeCategory m_timeCategory = TimeCategory.Environment;

        [SerializeField]
        [Tooltip("When the animation will be updated.")]
        private UpdateOn m_updateOn = UpdateOn.FixedUpdate;

        private readonly SystemRef<TimeManager> m_timeManagerRef = ProcessManager.GetSystemRef<TimeManager>();

        public bool IsPaused { get; set; }

        private void Awake()
        {
            foreach (SurfaceAnimatedKnot knot in m_knots)
                knot.CalculateCachedValues();
        }

        private void OnEnable()
        {
            m_timeManagerRef.InvokeOnValid(timeManager => timeManager.Subscribe(this, m_timeCategory, m_updateOn));
        }

        // 06003d18 (0x6D2BE0): the reference itself is null-checked before its system.
        private void OnDisable()
        {
            if (m_timeManagerRef != null && !m_timeManagerRef.IsNull())
                m_timeManagerRef.Get().Unsubscribe(this, m_timeCategory, m_updateOn);
        }

        public void OnUpdate(float deltaTime)
        {
            DoUpdate(deltaTime);
        }

        public void OnFixedUpdate(float deltaTime)
        {
            DoUpdate(deltaTime);
        }

        public void OnLateUpdate(float deltaTime)
        {
            DoUpdate(deltaTime);
        }

        private void DoUpdate(float deltaTime)
        {
            foreach (SurfaceAnimatedKnot knot in m_knots)
                knot.DoUpdate(deltaTime);
        }
    }
}
