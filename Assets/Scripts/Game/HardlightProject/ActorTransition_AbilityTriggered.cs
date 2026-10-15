using Unity.IL2CPP.CompilerServices;
using System;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000071; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("AbilityTriggered")]
	[GraphNodeMenuFormat("Actor/{0}")]
	public class ActorTransition_AbilityTriggered : FSMTransition
	{
		[Serializable]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[FiniteStateMachineJsonCtorArgs]
		protected class AbilityTriggeredJSONCtorArgs
		{
			[GraphUnityObject(typeof(AbilityDefinition))]
			public string AbilityDefinitionName;

			// Stored before the base constructor runs (0x5299B4).
			public bool IsTriggered = true;

			// 0600045c (1.10.1 RVA 0x5299B0)
			public AbilityTriggeredJSONCtorArgs()
			{
			}
		}

		private readonly AbilityDefinition m_abilityDefinition;

		private readonly bool m_isTriggered;

		// 06000459 (1.10.1 RVA 0x529740)
		protected ActorTransition_AbilityTriggered(FiniteStateMachine fsm, FSMIdentifier transitionId, AbilityTriggeredJSONCtorArgs ctorArgs) : base(fsm, transitionId)
		{
			m_isTriggered = ctorArgs.IsTriggered;
			if (DataManagerUtilities.TryGetAbilityDefinition(ctorArgs.AbilityDefinitionName, out m_abilityDefinition, "Transition " + transitionId.Name))
			{
				// The original reads the virtual ScriptType on success and discards it (0x5297E7).
				_ = m_abilityDefinition.ScriptType;
			}
		}

		// 0600045a (1.10.1 RVA 0x529800)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new ActorTransition_AbilityTriggered(fsm, transitionId, JsonUtility.FromJson<AbilityTriggeredJSONCtorArgs>(jsonCtorArgs));
		}

		// 0600045b (1.10.1 RVA 0x529890)
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			if (base.DoUpdate(user, updateContext))
			{
				return true;
			}
			ActorAbility ability;
			user.GetAs<Actor>().TryGetAbilityFromType(m_abilityDefinition.AbilityType, out ability);
			IAbilityTrigger trigger = ability as IAbilityTrigger;
			return trigger != null && m_isTriggered == trigger.IsTriggered;
		}
	}
}
