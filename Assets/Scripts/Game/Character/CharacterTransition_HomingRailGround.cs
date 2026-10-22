using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020003e7; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Character/{0}")]
	[GraphNodeDefaultName("ToHomingRailGround")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterTransition_HomingRailGround : CharacterTransition_Homing
	{
		// 0600174c (1.10.1 RVA 0x791C50)
		protected CharacterTransition_HomingRailGround(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
			
		}

		// 0600174d (1.10.1 RVA 0x791C60)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_HomingRailGround(fsm, transitionId);
		}

		// 0600174e (1.10.1 RVA 0x791CC0)
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			bool result = base.DoUpdate(user, updateContext);
			if (result)
			{
				user.GetAs<Character>().Storage.SetValue(ActorFSMKeys.RailTransferYVelocity, true);
			}
			return result;
		}

		// 0600174f (1.10.1 RVA 0x791EA0)
		protected override bool TargetIsValid(CharacterTargetObjectData targetObject)
		{
			return targetObject.Target != null && targetObject.Type == HomingTargetType.Rail;
		}
	}
}
