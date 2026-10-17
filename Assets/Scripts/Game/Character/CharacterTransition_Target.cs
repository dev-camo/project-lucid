using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000406; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class CharacterTransition_Target : FSMTransition
	{
		// 0600179d (1.10.1 RVA 0x793380)
		protected CharacterTransition_Target(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
		}

		// 0600179e (1.10.1 RVA 0x794770)
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			if (base.DoUpdate(user, updateContext))
				return true;
			Character character = user.GetAs<Character>();
			// The locked-on target, not the pending candidate; no active target reads as a null object.
			ActiveHomingTarget activeTarget = character.HomingPool.ActiveTarget;
			GameObject targetObject = activeTarget != null ? activeTarget.Object.Target : null;
			if (targetObject == null || !IsValidType(activeTarget.Object.Type))
				return false;
			if ((activeTarget.Object.WorldPosition - character.WorldPosition).sqrMagnitude > character.Constants.HomingTargetHitDistance)
				return false;
			ISurface surface = activeTarget.Object.Target.GetComponent<ISurface>();
			return surface != null && !ProcessManager.IsSystemNull<TrackManager>() && IsValidSurface(character.TrackManager, surface);
		}

		protected abstract bool IsValidType(HomingTargetType type);

		protected abstract bool IsValidSurface(TrackManager trackManager, ISurface surface);
	}
}
