using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000234; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Boss/{0}")]
	// The original shares the StartWindUp default name with BossTransition_StartWindUp (metadata blob); not a copy slip.
	[GraphNodeDefaultName("StartWindUp")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class BossTransition_AttackWindUpComplete : Transition_Ability
	{
		// 06000c49 (1.10.1 RVA 0x70E010)
		protected BossTransition_AttackWindUpComplete(FiniteStateMachine fsm, FSMIdentifier transitionId, AbilityJSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
		}

		// 06000c4a (1.10.1 RVA 0x70E020)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new BossTransition_AttackWindUpComplete(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}

		// 06000c4b (1.10.1 RVA 0x70E0B0): the base update is not consulted. The configured ability, when the boss has one that is
		// offensive, must report its wind-up as complete.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			user.GetAs<Boss>().TryGetAbility(m_abilityType, out ActorAbility ability);
			return ability is IOffensiveAbility offensiveAbility && offensiveAbility.WoundUp;
		}
	}
}
