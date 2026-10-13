using Unity.IL2CPP.CompilerServices;
using System;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020003c5; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[GraphNodeMenuFormat("Character/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("AirAbilityActiveRecently")]
	public class CharacterTransition_AirAbilityActiveRecently : FSMTransition
	{
		[Serializable]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		[FiniteStateMachineJsonCtorArgs]
		protected class AirAbilityActiveRecentlyJSONCtorArgs
		{
			[GraphUnityObject(typeof(CharacterAbilityDefinition_Air))]
			public string AirAbilityDefinitionName;

			public float ActiveWithinSeconds;

			public bool IncludeCurrentlyActive;

			public bool InverseCheck;

			// 060016fa (1.10.1 RVA 0x78EF50)
			public AirAbilityActiveRecentlyJSONCtorArgs()
			{
			}
		}

		private readonly CharacterAbilityDefinition_Air m_airAbilityDefinition;

		private readonly float m_activeWithinSeconds;

		private readonly bool m_includeCurrentlyActive;

		private readonly bool m_inverseCheck;

		// 060016f7 (1.10.1 RVA 0x78EC70)
		protected CharacterTransition_AirAbilityActiveRecently(FiniteStateMachine fsm, FSMIdentifier transitionId, AirAbilityActiveRecentlyJSONCtorArgs ctorArgs) : base(fsm, transitionId)
		{
			m_activeWithinSeconds = ctorArgs.ActiveWithinSeconds;
			m_includeCurrentlyActive = ctorArgs.IncludeCurrentlyActive;
			m_inverseCheck = ctorArgs.InverseCheck;
			DataManagerUtilities.TryGetAbilityDefinition(ctorArgs.AirAbilityDefinitionName, out AbilityDefinition definition, "Transition " + transitionId.Name);
			m_airAbilityDefinition = definition as CharacterAbilityDefinition_Air;
		}

		// 060016f8 (1.10.1 RVA 0x78EDC0)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_AirAbilityActiveRecently(fsm, transitionId, JsonUtility.FromJson<AirAbilityActiveRecentlyJSONCtorArgs>(jsonCtorArgs));
		}

		// 060016f9 (1.10.1 RVA 0x78EE50)
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			if (base.DoUpdate(user, updateContext))
				return true;

			Character character = user.GetAs<Character>();
			character.TryGetAbilityFromType(m_airAbilityDefinition.AbilityType, out ActorAbility ability);
			if (!(ability is CharacterAbility_Air airAbility))
				return false;

			bool active = airAbility.AirActive ? m_includeCurrentlyActive : airAbility.WasRecentlyActive(m_activeWithinSeconds);
			return m_inverseCheck ^ active;
		}
	}
}
