using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000243; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Boss/Guardian/{0}")]
	[GraphNodeDefaultName("GuardianWithinRange")]
	public class BossTransition_GuardianWIthinRange : Transition_Ability
	{
		// 06000c71 (1.10.1 RVA 0x70FD20)
		protected BossTransition_GuardianWIthinRange(FiniteStateMachine fsm, FSMIdentifier transitionId, AbilityJSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
		}

		// 06000c72 (1.10.1 RVA 0x70FD30)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new BossTransition_GuardianWIthinRange(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}

		// 06000c73 (1.10.1 RVA 0x70FDC0): the base update is not consulted. The guardian's stay-ahead ability decides
		// whether it should activate (the boss lies inside the arc ahead of the character); a boss without that ability
		// logs an error and is false.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			Boss boss = user.GetAs<Boss>();
			boss.TryGetAbility<BossAbility_StayAheadOfPlayer>(m_abilityType, out BossAbility_StayAheadOfPlayer ability);
			if (ability != null)
			{
				return ability.ShouldAbilityActivate();
			}
			HLOutput.LogError(string.Format("Ability {0} could not be found in {1} definition.", m_abilityType, boss), null);
			return false;
		}
	}
}
