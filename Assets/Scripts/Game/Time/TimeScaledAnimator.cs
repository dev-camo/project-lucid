using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime TimeScaledAnimator (TypeDef 0x020007f7, x86-64 0x5F90F0..0x5F940F): scales an
    // Animator's speed by the category's time scale and switches it off while paused.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TimeScaledAnimator : TimeScaledComponent_SDT
    {
        [SerializeField] private Animator m_animator;
        private bool m_wasEnabledOnPause;
        private float m_speed;

        protected override void OnValidate() // 06002e21
        {
            base.OnValidate();
            if (!gameObject.CanValidate() || m_animator != null) return;
            m_animator = GetComponent<Animator>();
        }

        public override bool CheckIsValid() { return m_animator != null; } // 06002e22

        protected override void Initialise(TimeManager timeManager) // 06002e23
        {
            if (CheckIsValid()) base.Initialise(timeManager);
        }

        // 06002e24: the speed is only written when it is not approximately the last one.
        protected override void InternalUpdate(float deltaTime)
        {
            float timescale = GetTimescale();
            if (Mathf.Approximately(timescale, m_speed)) return;
            m_speed = timescale;
            m_animator.speed = timescale;
        }

        // 06002e25 / 06002e26: the animator is disabled for the pause and re-enabled only if it was enabled.
        public override void OnPause()
        {
            base.OnPause();
            m_wasEnabledOnPause = m_animator.enabled;
            if (m_wasEnabledOnPause) m_animator.enabled = false;
        }

        public override void OnResume()
        {
            base.OnResume();
            if (m_wasEnabledOnPause) m_animator.enabled = true;
        }

        public TimeScaledAnimator() { } // 06002e27
    }
}
