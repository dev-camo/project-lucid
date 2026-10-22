using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020003cc; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeDefaultName("ToBossAttack")]
	[GraphNodeMenuFormat("Character/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterTransition_BossAttack : CharacterTransition_Homing
	{
		// 06001708 (1.10.1 RVA 0x78F700)
		protected CharacterTransition_BossAttack(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
		}

		// 06001709 (1.10.1 RVA 0x78F720)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_BossAttack(fsm, transitionId);
		}

		// 0600170a (1.10.1 RVA 0x78F780)
		protected override bool TargetIsValid(CharacterTargetObjectData targetObject)
		{
			return targetObject.Target != null && targetObject.Type == HomingTargetType.Boss;
		}
	}
}
