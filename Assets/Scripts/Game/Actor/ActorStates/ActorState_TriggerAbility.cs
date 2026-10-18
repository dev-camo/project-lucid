using Unity.IL2CPP.CompilerServices;
using System;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000064; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Actor/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeColour(1f, 0f, 0f)]
	[GraphNodeDefaultName("TriggerAbility")]
	public class ActorState_TriggerAbility : FSMState_Trigger
	{
		[Serializable]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		protected class JSONCtorArgs : TriggerJSONCtorArgs
		{
			[GraphUnityObject(typeof(AbilityDefinition))]
			public string AbilityDefinitionName;

			public bool Enable = true;

			// 0600043e (1.10.1 RVA 0x528280)
			public JSONCtorArgs()
			{
			}
		}

		private readonly AbilityDefinition m_abilityDefinition;

		private readonly bool m_enable;

		// 06000438 (1.10.1 RVA 0x527F50)
		protected ActorState_TriggerAbility(FiniteStateMachine fsm, FSMIdentifier stateId, JSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
			DataManagerUtilities.TryGetAbilityDefinition(ctorArgs.AbilityDefinitionName, out m_abilityDefinition, "State " + stateId.Name);
			m_enable = ctorArgs.Enable;
		}

		// 06000439 (1.10.1 RVA 0x528000)
		public static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new ActorState_TriggerAbility(fsm, transitionId, JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs));
		}

		// 0600043a (1.10.1 RVA 0x528100)
		protected override void InvokeTriggerStart(IGraphUser user, FSMStateChangeAction action)
		{
			TriggerAbility(user, m_enable);
		}

		// 0600043b (1.10.1 RVA 0x5281D0): leaving applies the opposite of the entry change.
		protected override void InvokeTriggerEnd(IGraphUser user, FSMStateChangeAction action)
		{
			TriggerAbility(user, !m_enable);
		}

		// 0600043c (1.10.1 RVA 0x528170)
		private void TriggerAbility(IGraphUser user, bool enable)
		{
			if (!GetActor(user).TryGetAbilityFromType(m_abilityDefinition.AbilityType, out ActorAbility ability))
			{
				return;
			}
			if (enable)
			{
				ability.Enter();
			}
			else
			{
				ability.Leave();
			}
		}

		// 0600043d (1.10.1 RVA 0x528240)
		protected virtual Actor GetActor(IGraphUser user)
		{
			return user.GetAs<Actor>();
		}
	}
}
