using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000070; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Actor/{0}")]
	[GraphNodeDefaultName("AbilityFinished")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ActorTransition_AbilityFinished : Transition_Ability
	{
		// 06000456 (1.10.1 RVA 0x529610)
		protected ActorTransition_AbilityFinished(FiniteStateMachine fsm, FSMIdentifier transitionId, AbilityJSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
		}

		// 06000457 (1.10.1 RVA 0x529620)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new ActorTransition_AbilityFinished(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}

		// 06000458 (1.10.1 RVA 0x5296B0). The base update is not consulted; a missing ability never finishes.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			user.GetAs<Actor>().TryGetAbility<ActorAbility>(m_abilityType, out ActorAbility ability);
			return ability != null && ability.AbilityFinished;
		}
	}
}
