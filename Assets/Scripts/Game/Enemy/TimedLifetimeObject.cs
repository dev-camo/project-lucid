using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0200067b, complete five-method owner.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TimedLifetimeObject : TimeScaledComponent_SDT
    {
        private float m_maxLifetime;
        private float m_currentLifetime;
        public bool m_initialised;

        // Original 06002314: equality is still alive; unordered comparisons
        // return false on both original architectures.
        public bool ObjectExpired => m_currentLifetime > m_maxLifetime;

        // Original 06002315: the damageable parameter is unused. The original
        // sets the flag before storing lifetime and does not reset elapsed time.
        public virtual void Initialise(IPlayerDamageable playerDamageable, float lifetime)
        {
            m_initialised = true;
            m_maxLifetime = lifetime;
        }

        // Original 06002316: the other fields remain untouched.
        public virtual void ResetTimedLifetimeObject()
        {
            m_currentLifetime = 0f;
        }

        // Original 06002317: only inherited pause state gates accumulation.
        protected override void InternalUpdate(float deltaTime)
        {
            if (!IsPaused)
                m_currentLifetime += deltaTime;
        }

        // Original 06002318 is the implicit constructor's sole base call.
    }
}
