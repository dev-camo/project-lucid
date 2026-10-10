using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ActorBuffHandler
    {
        private List<ActorBuff> m_buffs = new List<ActorBuff>();
        private Actor m_actor;

        public ActorBuffHandler(Actor actor) => m_actor = actor;
        public void AddBuff(ActorBuff buff) => m_buffs.Add(buff);

        // Original 06000468: visit the initial list size in reverse, run the
        // callback, then reload the list before removing the current index.
        public void OnUpdate(float deltaTime)
        {
            for (int i = m_buffs.Count - 1; i >= 0; --i)
            {
                ActorBuff buff = m_buffs[i];
                buff.OnUpdate(deltaTime);
                if (buff.Expired)
                    m_buffs.RemoveAt(i);
            }
        }

        // Original 06000469 matches the exact runtime type, not assignability.
        // It reloads Count on each iteration, allowing callbacks to add buffs.
        public void EndAllBuffsOfType<T>() where T : ActorBuff
        {
            for (int i = 0; i < m_buffs.Count; ++i)
            {
                ActorBuff buff = m_buffs[i];
                if (buff.GetType() == typeof(T))
                    buff.Expire();
            }
        }
    }
}
