using Unity.IL2CPP.CompilerServices;
using System;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200067d; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Generic/{0}")]
	[GraphNodeDefaultName("OverrideGamePlayControl")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class FSMStateOverrideGamePlayControl : FSMState
	{
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Serializable]
		private class JSONCtorArgs
		{
			public bool Override;

			// 06002325 (1.10.1 RVA 0x5842D0)
			public JSONCtorArgs()
			{
			}
		}

		private bool m_override { get; }

		// 06002322 (1.10.1 RVA 0x583D60)
		private FSMStateOverrideGamePlayControl(FiniteStateMachine fsm, FSMIdentifier stateId, bool setOverride) : base(fsm, stateId)
		{
			m_override = setOverride;
		}

		// 06002323 (1.10.1 RVA 0x583D80)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			return new FSMStateOverrideGamePlayControl(fsm, transitionId, args.Override);
		}

		// 06002324 (1.10.1 RVA 0x583E20): on the current character, the override turns controls off through
		// a stored modifier handle (adjusted if one is already stored); without the override a stored
		// handle is removed.
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			Character character = ProcessManager.GetSystem<CharacterManager>().GetCurrentCharacterUnsafe();
			if (m_override)
			{
				if (character.Storage.HasValue(ActorFSMKeys.InputOverrideModifierHandle))
				{
					StackableDataHandle existingHandle = character.Storage.GetValue<StackableDataHandle>(ActorFSMKeys.InputOverrideModifierHandle, null, true);
					character.AdjustModifierOverrides<bool>(existingHandle, (int)GameplayModifierType.ControlsEnabled, false);
				}
				else
				{
					StackableDataHandle handle = character.AddModifierOverride<bool>((int)GameplayModifierType.ControlsEnabled, false);
					character.Storage.SetValue(ActorFSMKeys.InputOverrideModifierHandle, handle);
				}
			}
			else if (character.Storage.HasValue(ActorFSMKeys.InputOverrideModifierHandle))
			{
				StackableDataHandle handle = character.Storage.GetValue<StackableDataHandle>(ActorFSMKeys.InputOverrideModifierHandle, null, true);
				character.RemoveModifierOverrides(handle);
				character.Storage.RemoveValue<StackableDataHandle>(ActorFSMKeys.InputOverrideModifierHandle);
			}
		}
	}
}
