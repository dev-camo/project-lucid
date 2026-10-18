using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020003c4; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Character/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterTransition_AirAbility : Transition_Ability
	{
		// 060016f4 (1.10.1 RVA 0x78EA90)
		protected CharacterTransition_AirAbility(FiniteStateMachine fsm, FSMIdentifier transitionId, AbilityJSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
		}

		// 060016f5 (1.10.1 RVA 0x78EAF0)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_AirAbility(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}

		// 060016f6 (1.10.1 RVA 0x78EB90). A secondary air ability that loses to an active primary is reset
		// without interruption and reports false.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			if (base.DoUpdate(user, updateContext))
			{
				return true;
			}
			if (!user.GetAs<Character>().TryGetAbility(m_abilityType, out CharacterAbility_Air ability))
			{
				return false;
			}
			if (ability.IsSecondaryAndPrimaryActive())
			{
				ability.Reset(false, false);
				return false;
			}
			return ability.AirActive;
		}
	}
}
