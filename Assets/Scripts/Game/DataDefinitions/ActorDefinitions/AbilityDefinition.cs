using System;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class AbilityDefinition : ScriptableObject
    {
        protected const string AssetMenu = "HardlightProject/DefinitionData/AbilityDef/";
        [HashEnum(typeof(ActorAbilityType))]
        public ActorAbilityType AbilityType;
        [Tooltip("Character will enter this form, if set, on entering the state.")]
        public ActorFormType FormType = ActorFormType.None;
        [Tooltip("Character will be locked to this form for the duration of the state or until lock is cleared.")]
        public bool FormIsLocked;
        [Tooltip("Defines motion overrides across different planes.")]
        public CollisionPlaneDefinition[] CollisionPlanes;
        [Tooltip("Passive abilities are not enabled/disabled specifically through fsm states.")]
        public bool Passive;
        [Tooltip("Enables ability on initialisation.")]
        public bool StartEnabled;
        [Tooltip("Overrides actor animation parameters.")]
        public ActorAnimationDefinition AnimationDefinition;
        [Tooltip("Overrides actor animation parameters for speed within limits.")]
        public IntervalList<float, ActorAnimationDefinition> AnimationDefinitionsBySpeed =
            new IntervalList<float, ActorAnimationDefinition>();

        // Original 0600196c: each request creates a real ability, initializes it,
        // then performs post-initialization before returning the same instance.
        public ActorAbility CreateAbility(Actor actor)
        {
            ActorAbility ability = Instantiate();
            ability.Initialise(actor, this);
            ability.PostInitialise();
            return ability;
        }

        public abstract Type ScriptType { get; }
        private ActorAbility Instantiate() => (ActorAbility)Activator.CreateInstance(ScriptType);

        // Original 0600196f is RET; derived definitions own their actual caches.
        public virtual void CalculateCachedValues() { }
        protected virtual void OnValidate() => CalculateCachedValues();

        // Original 06001971: a successful interval lookup wins even when its
        // stored animation is null; only a failed lookup uses the default field.
        public ActorAnimationDefinition GetAnimationDefinition(float speed)
        {
            ActorAnimationDefinition animation;
            return AnimationDefinitionsBySpeed.TryGetIntervalValue(speed, out animation)
                ? animation : AnimationDefinition;
        }

        protected AbilityDefinition() { }
    }
}
