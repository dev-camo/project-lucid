using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000242; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeDefaultName("GuardianAtMaxRange")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Boss/Guardian/{0}")]
	public class BossTransition_GuardianMaxRange : Transition_Ability
	{
		// 06000c6e (1.10.1 RVA 0x70FB70)
		protected BossTransition_GuardianMaxRange(FiniteStateMachine fsm, FSMIdentifier transitionId, AbilityJSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
		}

		// 06000c6f (1.10.1 RVA 0x70FB80)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new BossTransition_GuardianMaxRange(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}

		// 06000c70 (1.10.1 RVA 0x70FC10): the base update is not consulted. The guardian's stay-ahead ability reports whether
		// it is at its maximum range; a boss without that ability logs an error and is false.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			Boss boss = user.GetAs<Boss>();
			boss.TryGetAbility<BossAbility_StayAheadOfPlayer>(m_abilityType, out BossAbility_StayAheadOfPlayer ability);
			if (ability != null)
			{
				return ability.AtMaxRange;
			}
			HLOutput.LogError(string.Format("Ability {0} could not be found in {1} definition.", m_abilityType, boss), null);
			return false;
		}
	}
}
