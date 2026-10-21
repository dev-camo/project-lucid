using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime TimeScaledAnimation (TypeDef 0x020007f6, x86-64 0x5F8A80..0x5F90EF): scales the
    // speed of every state of a legacy Animation by the category's time scale.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TimeScaledAnimation : TimeScaledComponent_SDT, ITimeScaled
    {
        [SerializeField] private Animation m_animation;
        private float m_currentTimescale = 1f;

        protected override void OnValidate() // 06002e16
        {
            base.OnValidate();
            if (!gameObject.CanValidate() || m_animation != null) return;
            m_animation = GetComponent<Animation>();
        }

        public override bool CheckIsValid() { return m_animation != null; } // 06002e17

        // 06002e18: without an animation nothing subscribes, not even the synchronisation.
        protected override void Initialise(TimeManager timeManager)
        {
            if (CheckIsValid()) base.Initialise(timeManager);
        }

        protected override void InternalUpdate(float deltaTime) { UpdateTimescale(); } // 06002e19
        public override void OnPause() { base.OnPause(); UpdateTimescale(); } // 06002e1a
        public override void OnResume() { base.OnResume(); UpdateTimescale(); } // 06002e1b
        public void Play() { m_animation.Play(); } // 06002e1c

        public void PlayClip(AnimationClip clip) // 06002e1d
        {
            m_animation.clip = clip;
            m_animation.Play();
        }

        public void Rewind() { m_animation.Rewind(); } // 06002e1e

        // 06002e1f: only a playing animation whose scale moved by more than 0.0001 is rewritten.
        private void UpdateTimescale()
        {
            if (!m_animation.isPlaying) return;
            float timescale = GetTimescale();
            if (Mathf.Abs(timescale - m_currentTimescale) > 0.0001f)
            {
                foreach (AnimationState state in m_animation) state.speed = timescale;
                m_currentTimescale = timescale;
            }
        }

        public TimeScaledAnimation() { } // 06002e20: m_currentTimescale = 1 precedes the base constructor.
    }
}
