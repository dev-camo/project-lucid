using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200036b; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("GroundBraking")]
	[GraphNodeMenuFormat("Character/{0}")]
	public class CharacterState_GroundBraking : CharacterState_Ground<CharacterAbilityDefinition_Braking, CharacterAbility_Braking>
	{
		// 06001573 (1.10.1 RVA 0x7765E0)
		protected CharacterState_GroundBraking(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 06001574 (1.10.1 RVA 0x776650)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
		{
			return new CharacterState_GroundBraking(fsm, stateId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}

		// 06001575 (1.10.1 RVA 0x776710)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			user.GetAs<Character>().OrientateHeadingToForward();
		}
	}
}
