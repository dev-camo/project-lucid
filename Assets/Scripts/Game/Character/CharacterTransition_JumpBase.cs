using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020003eb; bodies translated from the 1.10.1 x86-64 binary (shared generic).
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public abstract class CharacterTransition_JumpBase<TAbility, TAbilityDef> : Transition_Ability
		where TAbility : CharacterAbility_MovementJumpBase<TAbilityDef>
		where TAbilityDef : CharacterAbilityDefinition_Jump
	{
		// Original nested 020003ec.
		[Il2CppSetOption(Option.NullChecks, false)]
		[FiniteStateMachineJsonCtorArgs]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Serializable]
		protected class JumpTransitionBaseJSONCtorArgs : AbilityJSONCtorArgs
		{
			public bool FaceVelocityDirection;

			// 0600175b (1.10.1 RVA 0x146EB40, shared generic)
			public JumpTransitionBaseJSONCtorArgs()
			{
			}
		}

		private readonly bool m_faceVelocityDirection;

		// 06001756 (1.10.1 RVA 0xE78C90, shared generic)
		protected CharacterTransition_JumpBase(FiniteStateMachine fsm, FSMIdentifier transitionId, JumpTransitionBaseJSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
			m_faceVelocityDirection = ctorArgs.FaceVelocityDirection;
		}

		// 06001757 (1.10.1 RVA 0xE78CC0, shared generic)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			user.Storage.RemoveValue<bool>(ActorFSMKeys.JumpFaceVelocityDirection);
		}

		// 06001758 (1.10.1 RVA 0xE78E50, shared generic); the result is recorded in storage only for transitions that face the velocity direction.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			bool triggered = InternalDoUpdate(user, updateContext);
			if (m_faceVelocityDirection)
			{
				user.Storage.SetValue(ActorFSMKeys.JumpFaceVelocityDirection, triggered);
			}
			return triggered;
		}

		// 06001759 (1.10.1 RVA 0xE78FF0, shared generic)
		private bool AbilityJumpTriggered(Character character)
		{
			return character.TryGetAbility(m_abilityType, out TAbility ability) && ability != null && ability.Jump;
		}

		// 0600175a (1.10.1 RVA 0xE79050, shared generic). A jump never triggers while a state timer runs, and one already in
		// progress (JumpElapsedTime > 0) returns true without recomputing the direction. Only a fresh trigger stores the jump
		// direction: the contact normal (or world up on rails, catch release and ledge buffers), rotated by JumpAngleExtra towards gravity.
		private bool InternalDoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			if (base.DoUpdate(user, updateContext))
			{
				return true;
			}
			Character character = user.GetAs<Character>();
			if (!AbilityJumpTriggered(character))
			{
				return false;
			}
			if (character.Storage.GetValue(ActorFSMKeys.StateRemainTimer, 0f, true) > 0f)
			{
				return false;
			}
			if (character.Storage.GetValue(ActorFSMKeys.JumpElapsedTime, 0f, true) > 0f)
			{
				return true;
			}
			bool catchReleaseActive = character.Storage.GetValue(ActorFSMKeys.CatchReleaseActive, false, true);
			bool jumpOnRailTracker = character.Storage.GetValue(ActorFSMKeys.JumpOnRailTracker, false, true);
			bool airLedgeBufferFromRail = character.Storage.GetValue(ActorFSMKeys.AirLedgeBufferFromRail, false, true);
			Vector3 jumpDirection = catchReleaseActive || jumpOnRailTracker || airLedgeBufferFromRail ? character.WorldUp : character.ActiveTracking.GetContactNormal();
			float angleExtra = character.Storage.GetValue(ActorFSMKeys.JumpAngleExtra, 0f, true);
			if (angleExtra != 0f)
			{
				jumpDirection = Vector3.RotateTowards(jumpDirection, character.Gravity, angleExtra * -Mathf.Deg2Rad, 0f);
			}
			character.Storage.SetValue(ActorFSMKeys.JumpDirection, jumpDirection);
			return true;
		}
	}
}
