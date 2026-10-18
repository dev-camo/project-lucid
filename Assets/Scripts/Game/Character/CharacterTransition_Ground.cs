using Unity.IL2CPP.CompilerServices;
using System;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020003d9; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Character/{0}")]
	[GraphNodeDefaultName("ToGround")]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterTransition_Ground : FSMTransition
	{
		// Original nested 020003da.
		[FiniteStateMachineJsonCtorArgs]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Serializable]
		private class JSONCtorArgs
		{
			public CharacterTrackingType TrackingType;

			// 0600172b (1.10.1 RVA 0x7907F0)
			public JSONCtorArgs()
			{
			}
		}

		private readonly CharacterTrackingType TrackingType;

		// 06001728 (1.10.1 RVA 0x790680)
		private CharacterTransition_Ground(FiniteStateMachine fsm, FSMIdentifier transitionId, JSONCtorArgs ctorArgs) : base(fsm, transitionId)
		{
			TrackingType = ctorArgs.TrackingType;
		}

		// 06001729 (1.10.1 RVA 0x7906B0)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_Ground(fsm, transitionId, JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs));
		}

		// 0600172a (1.10.1 RVA 0x790750); on the ground, but not while resting on a climbable surface.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			if (base.DoUpdate(user, updateContext))
			{
				return true;
			}
			Character character = user.GetAs<Character>();
			if (!character.IsOnGround(TrackingType))
			{
				return false;
			}
			return !(character.Collider.HasLinkedCollisionData && character.Collider.LinkedCollisionData.IsClimbable);
		}
	}
}
