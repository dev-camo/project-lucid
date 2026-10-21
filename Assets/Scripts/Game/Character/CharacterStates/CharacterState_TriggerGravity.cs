using Unity.IL2CPP.CompilerServices;
using System;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020003a0; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[GraphNodeMenuFormat("Character/Trigger/{0}")]
	[GraphNodeDefaultName("TriggerGravity")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterState_TriggerGravity : FSMState_Trigger
	{
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Serializable]
		protected class JSONCtorArgs : TriggerJSONCtorArgs
		{
			public bool GravityEnabled = true;

			// 06001634 (1.10.1 RVA 0x783880)
			public JSONCtorArgs()
			{
			}
		}

		private readonly bool m_gravityEnabled;

		// 06001631 (1.10.1 RVA 0x783350)
		protected CharacterState_TriggerGravity(FiniteStateMachine fsm, FSMIdentifier stateId, JSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
			m_gravityEnabled = ctorArgs.GravityEnabled;
		}

		// 06001632 (1.10.1 RVA 0x783380)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_TriggerGravity(fsm, transitionId, JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs));
		}

		// 06001633 (1.10.1 RVA 0x783420); any gravity override this state placed earlier is replaced, and a new
		// disabling override is added only when gravity is to be switched off.
		protected override void InvokeTriggerStart(IGraphUser user, FSMStateChangeAction action)
		{
			Character character = user.GetAs<Character>();
			StackableDataHandle handle = user.Storage.GetValue<StackableDataHandle>(ActorFSMKeys.GravityDisabledModifierHandle, null, true);
			if (handle != null)
			{
				character.RemoveModifierOverrides(handle);
				user.Storage.RemoveValue<StackableDataHandle>(ActorFSMKeys.GravityDisabledModifierHandle);
			}
			if (!m_gravityEnabled)
			{
				handle = character.AddModifierOverride((int)GameplayModifierType.ApplyGravity, false);
				user.Storage.SetValue(ActorFSMKeys.GravityDisabledModifierHandle, handle);
			}
		}
	}
}
