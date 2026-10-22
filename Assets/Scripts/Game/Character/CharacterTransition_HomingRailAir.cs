using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020003e6; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeDefaultName("ToHomingRailAir")]
	[GraphNodeMenuFormat("Character/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterTransition_HomingRailAir : CharacterTransition_Homing
	{
		// 06001748 (1.10.1 RVA 0x7918D0)
		protected CharacterTransition_HomingRailAir(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
			
		}

		// 06001749 (1.10.1 RVA 0x7918E0)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_HomingRailAir(fsm, transitionId);
		}

		// 0600174a (1.10.1 RVA 0x791940)
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			bool result = base.DoUpdate(user, updateContext);
			if (result)
			{
				Character character = user.GetAs<Character>();
				bool jumpedFromRail = character.Storage.GetValue(ActorFSMKeys.JumpOnRailTracker, false, true);
				character.Storage.SetValue(ActorFSMKeys.RailTransferYVelocity, !jumpedFromRail);
			}
			return result;
		}

		// 0600174b (1.10.1 RVA 0x791BE0)
		protected override bool TargetIsValid(CharacterTargetObjectData targetObject)
		{
			return targetObject.Target != null && targetObject.Type == HomingTargetType.Rail;
		}
	}
}
