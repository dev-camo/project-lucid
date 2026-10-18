using Hardlight;
using HardlightProject;

namespace ProjectLucid.Tests.WorldTriggersShared
{
    // Test-only Transition_Ability, as the shipped ability transitions derive from it. The
    // nested JSON constructor arguments are protected, so only a descendant can build them.
    public sealed class TransitionAbilityProbe : Transition_Ability
    {
        public TransitionAbilityProbe(FiniteStateMachine fsm, FSMIdentifier transitionId, ActorAbilityType abilityType)
            : base(fsm, transitionId, new AbilityJSONCtorArgs { AbilityType = abilityType })
        {
        }

        public ActorAbilityType Type => m_abilityType;

        public static System.Type ArgumentsType => typeof(AbilityJSONCtorArgs);
    }
}
