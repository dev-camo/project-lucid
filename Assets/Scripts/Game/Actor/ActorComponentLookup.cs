// Complete original Game.Runtime02000051, all7fields/9native body candidates.
// Genuine real Actor/audio/PFX/footstep dependencies remain required. Unaccepted.
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks,false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks,false)]
    public class ActorComponentLookup : MonoBehaviour
    {
        [SerializeField] private Animator m_animator;
        [SerializeField] private ActorAudio m_actorAudio;
        [SerializeField] private ActorAttachPoints m_actorAttachPoints;
        [SerializeField] private ActorPFXController m_actorPFXController;
        [SerializeField] private ActorFormHandler m_actorFormHandler;
        [SerializeField] private ActorFootsteps m_actorFootsteps;
        private Actor m_actor;
        public Animator Animator { get { return m_animator; } } // original0600039f
        public ActorAudio ActorAudio { get { return m_actorAudio; } }
        public ActorAttachPoints ActorAttachPoints { get { return m_actorAttachPoints; } }
        public ActorPFXController ActorPFXController { get { return m_actorPFXController; } }
        public ActorFormHandler ActorFormHandler { get { return m_actorFormHandler; } }
        public ActorFootsteps ActorFootsteps { get { return m_actorFootsteps; } }
        public Actor Actor { get { return m_actor; } } // original060003a5
        public void Initialise(Actor actor) { m_actor = actor; } // original060003a6
        public ActorComponentLookup() { } // genuine base-only original060003a7
    }
}
