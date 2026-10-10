using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ActorBuff
    {
        public bool Expired { get; private set; }
        protected readonly float m_durationSeconds;
        protected readonly Actor m_actor;
        protected float m_currentTimeSeconds;

        protected ActorBuff(Actor actor, float durationSeconds)
        {
            m_actor = actor;
            m_durationSeconds = durationSeconds;
            m_currentTimeSeconds = 0f;
        }

        // Original 06000464: the updated binary32 accumulator is stored before
        // comparing with duration, and expiration dispatches the virtual hook.
        public virtual void OnUpdate(float deltaTime)
        {
            if (Expired)
                return;
            m_currentTimeSeconds += deltaTime;
            if (m_currentTimeSeconds >= m_durationSeconds)
                Expire();
        }

        public virtual void Expire() => Expired = true;
    }
}
