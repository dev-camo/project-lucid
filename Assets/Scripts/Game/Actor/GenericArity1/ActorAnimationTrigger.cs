using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200076a; shared generic bodies at 0xF19E70..0xF1A480.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class ActorAnimationTrigger<T> : MonoBehaviour where T : Actor
    {
        [SerializeField]
        private ActorAnimationDefinition m_animation;
        [SerializeField]
        [Tooltip("If true, only plays TriggerAnimationEnter for actors, and does not keep a list of actors currently playing.")]
        private bool m_onEnterOnly;
        private readonly List<T> m_actorsPendingAnimationLeave = new List<T>();
        private readonly Dictionary<T, List<FullscreenShaderManager.ParametersHandle>> m_actorEffectDictionary =
            new Dictionary<T, List<FullscreenShaderManager.ParametersHandle>>();
        private bool m_enabled;

        public void OnEnable()
        {
            m_actorsPendingAnimationLeave.Clear();
            m_actorEffectDictionary.Clear();
            m_enabled = true;
        }

        // Actors still playing get their leave animation before the bookkeeping is dropped.
        public void OnDisable()
        {
            if (!m_onEnterOnly)
            {
                foreach (T actor in m_actorsPendingAnimationLeave)
                {
                    m_actorEffectDictionary.TryGetValue(actor, out List<FullscreenShaderManager.ParametersHandle> handles);
                    actor.TriggerAnimationLeave(m_animation, handles);
                }
            }
            m_actorEffectDictionary.Clear();
            m_actorsPendingAnimationLeave.Clear();
            m_enabled = false;
        }

        public void Action_OnEnter(T actor)
        {
            if (m_enabled && FilterActor(actor))
                TryPlayAnimation(actor);
        }

        // Lookups whose actor is not a T are ignored.
        public void Action_OnEnter(ActorComponentLookup actorComponentLookup)
        {
            if (m_enabled)
            {
                T actor = actorComponentLookup.Actor as T;
                if (actor != null)
                    Action_OnEnter(actor);
            }
        }

        protected abstract bool FilterActor(T actor);

        public void Action_OnLeave(T actor)
        {
            List<FullscreenShaderManager.ParametersHandle> handles = null;
            if (m_enabled && !m_onEnterOnly && m_actorsPendingAnimationLeave.Remove(actor))
            {
                m_actorEffectDictionary.TryGetValue(actor, out handles);
                actor.TriggerAnimationLeave(m_animation, handles);
            }
        }

        public void Action_OnLeave(ActorComponentLookup actorComponentLookup)
        {
            if (m_enabled)
            {
                T actor = actorComponentLookup.Actor as T;
                if (actor != null)
                    Action_OnLeave(actor);
            }
        }

        // An actor already playing is not started twice unless only entries are tracked.
        private void TryPlayAnimation(T actor)
        {
            if (m_onEnterOnly || !m_actorsPendingAnimationLeave.Contains(actor))
            {
                List<FullscreenShaderManager.ParametersHandle> handles = m_actorEffectDictionary.TryGetOrNew(actor);
                actor.TriggerAnimationEnter(m_animation, handles);
                if (!m_onEnterOnly)
                    m_actorsPendingAnimationLeave.Add(actor);
            }
        }
    }
}
