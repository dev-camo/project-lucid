using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "ActorParticleEffectDefinition", menuName = "HardlightProject/DefinitionData/Definitions/ActorParticleEffectDefinition")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ActorParticleEffectDefinition : ParticleEffectDefinition
    {
        [Tooltip("If true, pfx will be stopped if active when triggered again. NB: If true, pool size should always be set to 1."), SerializeField]
        private bool m_canInterrupt;
        [Tooltip("Defines the parent location, if any, that this pfx will sit under."), SerializeField, HashEnum(typeof(ActorAttachPointType))]
        private ActorAttachPointType m_location;
        [SerializeField, Tooltip("If true, then if the associated Actor is invisible (e.g. due to flickering during invincibility), the particle effect will be made invisible too.")]
        private bool m_syncVisibilityWithActor;
        [SerializeField, Tooltip("If true, orientation is set in the direction of the actor's current velocity.")]
        private bool m_syncOrientationWithActorVelocity;
        [Tooltip("If true this will attach to the actor, set position and rotation then detach with the below options"), SerializeField]
        private bool m_detachImmediately;
        [SerializeField, ShowIf("m_detachImmediately", (object)null)] private bool m_keepPosition;
        [ShowIf("m_detachImmediately", (object)null), SerializeField] private bool m_keepRotation;

        // Game.Runtime 06001e84..8a; original own fields, no computed fallback.
        public bool CanInterrupt => m_canInterrupt;
        public ActorAttachPointType Location => m_location;
        public bool SyncVisibilityWithActor => m_syncVisibilityWithActor;
        public bool SyncOrientationWithActorVelocity => m_syncOrientationWithActorVelocity;
        public bool DetachImmediately => m_detachImmediately;
        public bool KeepPosition => m_keepPosition;
        public bool KeepRotation => m_keepRotation;
        protected override void UpdateCachedValues() { base.UpdateCachedValues(); } // 06001e8b.
        public ActorParticleEffectDefinition() { } // 06001e8c, genuine base initialization.
    }
}
