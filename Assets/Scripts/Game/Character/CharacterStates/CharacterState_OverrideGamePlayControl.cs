using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000396; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeDefaultName("OverrideGamePlayControl")]
	[GraphNodeMenuFormat("Character/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterState_OverrideGamePlayControl : ActorState_Effect
	{
		// 06001612 (1.10.1 RVA 0x781E80)
		protected CharacterState_OverrideGamePlayControl(FiniteStateMachine fsm, FSMIdentifier stateId, BaseJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 06001613 (1.10.1 RVA 0x781E90)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_OverrideGamePlayControl(fsm, transitionId, JsonUtility.FromJson<BaseJSONCtorArgs>(jsonCtorArgs));
		}

		// 06001614 (1.10.1 RVA 0x781F20)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			Character character = user.GetAs<Character>();
			StackableDataHandle handle = character.AddModifierOverride<bool>((int)GameplayModifierType.ControlsEnabled, false);
			character.Storage.SetValue(ActorFSMKeys.InputOverrideModifierHandle, handle);
		}

		// 06001615 (1.10.1 RVA 0x782080)
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnLeave(user, action);
			Character character = user.GetAs<Character>();
			StackableDataHandle handle = character.Storage.GetValue<StackableDataHandle>(ActorFSMKeys.InputOverrideModifierHandle, null, true);
			character.RemoveModifierOverrides(handle);
			character.Storage.RemoveValue<StackableDataHandle>(ActorFSMKeys.InputOverrideModifierHandle);
		}
	}
}
