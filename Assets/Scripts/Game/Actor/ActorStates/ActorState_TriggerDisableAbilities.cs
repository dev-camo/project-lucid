using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000066; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Actor/{0}")]
	[GraphNodeDefaultName("TriggerDisableAbilities")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class ActorState_TriggerDisableAbilities : FSMState_Trigger
	{
		[Serializable]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		protected class JSONCtorArgs : TriggerJSONCtorArgs
		{
			[GraphUnityObject(typeof(AbilityDefinition))]
			public List<string> ExceptionList;

			// 06000443 (1.10.1 RVA 0x5287E0)
			public JSONCtorArgs()
			{
			}
		}

		private readonly List<AbilityDefinition> m_exceptionList = new List<AbilityDefinition>();

		// 0600043f (1.10.1 RVA 0x528290). Names without a registered definition are skipped.
		protected ActorState_TriggerDisableAbilities(FiniteStateMachine fsm, FSMIdentifier stateId, JSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
			// The native body fetches the App reference here and discards it.
			ProcessManager.GetSystemRef<App>().Get();
			DataManager dataManager = ProcessManager.GetSystemSafe<DataManager>();
			foreach (string abilityName in ctorArgs.ExceptionList)
			{
				if (dataManager.AbilityDefinitions.TryGetValue(abilityName, out AbilityDefinition abilityDefinition))
				{
					m_exceptionList.Add(abilityDefinition);
				}
			}
		}

		// 06000440 (1.10.1 RVA 0x528590)
		public static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new ActorState_TriggerDisableAbilities(fsm, transitionId, JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs));
		}

		// 06000441 (1.10.1 RVA 0x528620). Leaves every non-passive ability whose script type is not in the exception list.
		protected override void InvokeTriggerStart(IGraphUser user, FSMStateChangeAction action)
		{
			user.GetAs<Actor>().GetAbilitiesOfType<ActorAbility>(actorAbility =>
			{
				if (actorAbility.DefinitionBase.Passive)
				{
					return;
				}
				if (m_exceptionList.Exists(abilityDefinition => actorAbility.DefinitionBase.ScriptType == abilityDefinition.ScriptType))
				{
					return;
				}
				actorAbility.Leave();
			});
		}
	}
}
