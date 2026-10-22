using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020003e8; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeDefaultName("ToHomingTransporter")]
	[GraphNodeMenuFormat("Character/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterTransition_HomingTransporter : CharacterTransition_Homing
	{
		// 06001750 (1.10.1 RVA 0x791F10)
		protected CharacterTransition_HomingTransporter(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
			
		}

		// 06001751 (1.10.1 RVA 0x791F20)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_HomingTransporter(fsm, transitionId);
		}

		// 06001752 (1.10.1 RVA 0x791F80)
		protected override bool TargetIsValid(CharacterTargetObjectData targetObject)
		{
			return targetObject.Target != null && targetObject.Type == HomingTargetType.Transporter;
		}
	}
}
