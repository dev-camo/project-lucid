using Unity.IL2CPP.CompilerServices;
using System;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020003b0; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("TriggerStickyControls")]
	[GraphNodeMenuFormat("Character/Trigger/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterState_TriggerStickyControls : FSMState_Trigger
	{
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Serializable]
		protected class JSONCtorArgs : TriggerJSONCtorArgs
		{
			[GraphUnityObject(typeof(CharacterStickyControlsDefinition))]
			public string StickyControls;

			// 0600166a (1.10.1 RVA 0x785B20)
			public JSONCtorArgs()
			{
			}
		}

		private readonly CharacterStickyControlsDefinition m_stickyControlsDefinition;

		// 06001666 (1.10.1 RVA 0x7855C0); the lookup result is not checked, a missing definition leaves the field null.
		protected CharacterState_TriggerStickyControls(FiniteStateMachine fsm, FSMIdentifier stateId, JSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
			DataManagerUtilities.TryGetStickyControlsDefinition(ctorArgs.StickyControls, out m_stickyControlsDefinition, "State " + stateId.Name);
		}

		// 06001667 (1.10.1 RVA 0x785660)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_TriggerStickyControls(fsm, transitionId, JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs));
		}

		// 06001668 (1.10.1 RVA 0x785750)
		protected override void InvokeTriggerStart(IGraphUser user, FSMStateChangeAction action)
		{
			m_stickyControlsDefinition.StickyControls.DoTriggerActivate(user.GetAs<Character>());
		}

		// 06001669 (1.10.1 RVA 0x7859C0)
		protected override void InvokeTriggerEnd(IGraphUser user, FSMStateChangeAction action)
		{
			m_stickyControlsDefinition.StickyControls.DoTriggerDeactivate(user.GetAs<Character>());
		}
	}
}
