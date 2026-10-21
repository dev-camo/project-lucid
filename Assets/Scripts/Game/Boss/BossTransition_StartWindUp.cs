using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200024e; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("StartWindUp")]
	[GraphNodeMenuFormat("Boss/{0}")]
	public class BossTransition_StartWindUp : Transition_Ability
	{
		// 06000c90 (1.10.1 RVA 0x710F00)
		protected BossTransition_StartWindUp(FiniteStateMachine fsm, FSMIdentifier transitionId, AbilityJSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
		}

		// 06000c91 (1.10.1 RVA 0x710F10)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new BossTransition_StartWindUp(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}

		// 06000c92 (1.10.1 RVA 0x710FA0): as the wind-up complete check, but reads StartWindUp.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			user.GetAs<Boss>().TryGetAbility(m_abilityType, out ActorAbility ability);
			return ability is IOffensiveAbility offensiveAbility && offensiveAbility.StartWindUp;
		}
	}
}
