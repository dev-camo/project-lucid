using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020003df; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Character/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("HasAbilityType")]
	public class CharacterTransition_HasAbilityType : Transition_Ability
	{
		// Original nested 020003e0.
		[Il2CppSetOption(Option.NullChecks, false)]
		[FiniteStateMachineJsonCtorArgs]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Serializable]
		protected class HasAbilityTypeJSONCtorArgs : AbilityJSONCtorArgs
		{
			public bool InverseCheck;

			// 0600173b (1.10.1 RVA 0x7914B0)
			public HasAbilityTypeJSONCtorArgs()
			{
			}
		}

		private readonly bool m_inverseCheck;

		// 06001738 (1.10.1 RVA 0x791360)
		protected CharacterTransition_HasAbilityType(FiniteStateMachine fsm, FSMIdentifier transitionId, HasAbilityTypeJSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
			m_inverseCheck = ctorArgs.InverseCheck;
		}

		// 06001739 (1.10.1 RVA 0x791390)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_HasAbilityType(fsm, transitionId, JsonUtility.FromJson<HasAbilityTypeJSONCtorArgs>(jsonCtorArgs));
		}

		// 0600173a (1.10.1 RVA 0x791430); true when the ability presence differs from the inverse flag.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			if (base.DoUpdate(user, updateContext))
			{
				return true;
			}
			return user.GetAs<Character>().HasAbilityType(m_abilityType) != m_inverseCheck;
		}
	}
}
