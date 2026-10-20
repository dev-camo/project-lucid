using System;
using System.Text;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200037e; bodies translated from the 1.10.1 x86-64 binary (shared generic).
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class CharacterState_JumpBase<TAbility, TAbilityDef> : CharacterState_Ability<TAbility>
		where TAbility : CharacterAbility_MovementJumpBase<TAbilityDef>
		where TAbilityDef : CharacterAbilityDefinition_Jump
	{
		// Original nested 0200037f.
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[FiniteStateMachineJsonCtorArgs]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Serializable]
		protected class JumpBaseJSONCtorArgs : AbilityJSONCtorArgs
		{
			public ActorAbilityType AirMovementAbilityType;

			public string BallToBallAnimationBool;

			// 060015b6 (1.10.1 RVA 0x146EB30, shared generic)
			public JumpBaseJSONCtorArgs()
			{
			}
		}

		private readonly ActorAbilityType m_airMovementAbilityType;

		private readonly int m_ballToBallAnimationBoolId;

		// 060015b0 (1.10.1 RVA 0xE74010, shared generic)
		protected CharacterState_JumpBase(FiniteStateMachine fsm, FSMIdentifier stateId, JumpBaseJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
			m_airMovementAbilityType = ctorArgs.AirMovementAbilityType;
			if (!string.IsNullOrWhiteSpace(ctorArgs.BallToBallAnimationBool))
			{
				m_ballToBallAnimationBoolId = Animator.StringToHash(ctorArgs.BallToBallAnimationBool);
			}
		}

		// 060015b1 (1.10.1 RVA 0xE74070, shared generic). When its id is non-zero the ball to ball animation bool is
		// written on every entry: true only when the form was Ball both before and after the base entry, false otherwise.
		// The jump adds the initial velocity along the jump direction (plus the part of the connected collider velocity that
		// opposes it) and lifts the upward speed to at least the configured minimum.
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			Character character = user.GetAs<Character>();
			ActorFormType previousFormType = character.FormType;
			base.DoOnEnter(user, action);
			if (m_ballToBallAnimationBoolId != 0)
			{
				bool ballToBall = previousFormType == ActorFormType.Ball && character.FormType == ActorFormType.Ball;
				character.Animator.SetAnimation(m_ballToBallAnimationBoolId, ballToBall);
			}
			character.OnMoveAwayFromGround();
			character.Storage.SetValue(ActorFSMKeys.IsJumping, true);
			Vector3 jumpDirection = character.Storage.GetValue(ActorFSMKeys.JumpDirection, default(Vector3), true);
			TAbility ability = GetAbility(character);
			Vector3 velocity = character.WorldVelocity;
			velocity += jumpDirection * ability.Definition.JumpInitialVelocity;
			float connectionAlongJump = Vector3.Dot(character.Collider.ConnectionTracker.Velocity, jumpDirection);
			if (connectionAlongJump < 0f)
			{
				velocity -= jumpDirection * connectionAlongJump;
			}
			Vector3 worldUp = character.WorldUp;
			float upwardSpeed = Vector3.Dot(velocity, worldUp);
			float upMin = ability.Definition.JumpInitialVelocityUpMin;
			if (upMin > upwardSpeed)
			{
				velocity += worldUp * (upMin - upwardSpeed);
			}
			character.SetWorldVelocity(velocity);
			if (character.Storage.GetValue(ActorFSMKeys.JumpFaceVelocityDirection, false, false))
			{
				Vector3 planarVelocity = Vector3.ProjectOnPlane(character.WorldVelocity, worldUp);
				if (planarVelocity != Vector3.zero)
				{
					character.OrientateToPlaneWithLookDirection(worldUp, planarVelocity);
				}
			}
		}

		// 060015b2 (1.10.1 RVA 0xE74720, shared generic). Before the minimum jump time the jump always continues; after it only
		// while the jump is held and the maximum jump time has not been reached. Leaving the ground starts the exited ground flag.
		protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			base.DoUpdate(user, updateContext);
			Character character = user.GetAs<Character>();
			TAbility ability = GetAbility(character);
			TAbilityDef definition = ability.Definition;
			CharacterAbility_MovementAir airAbility = character.GetAbility<CharacterAbility_MovementAir>(m_airMovementAbilityType);
			CharacterAbilityDefinition_MovementAir airDefinition = airAbility.Definition;
			bool maintainHeading = MaintainHeading(ability);
			bool decelerationDisabled = character.Storage.GetValue(ActorFSMKeys.JumpDecelerationDisabled, false, true);
			CharacterMovementUtilities.ProcessAirControl(character, ability, airDefinition, maintainHeading, updateContext.DeltaTime, !decelerationDisabled);
			character.Tracker.ClampToLateralBoundsViaSurfaceRaycast();
			float jumpElapsed = character.Storage.AdjustValue(ActorFSMKeys.JumpElapsedTime, updateContext.DeltaTime, 0f);
			float jumpTimer = jumpElapsed - definition.JumpDelaySeconds;
			bool jumping;
			if (!(jumpTimer >= definition.MinimumJumpTimeSeconds))
			{
				jumping = true;
			}
			else
			{
				jumping = ability.Jump && !(jumpTimer >= definition.MaximumJumpTimeSeconds);
			}
			if (!jumping)
			{
				character.Storage.SetValue(ActorFSMKeys.IsJumping, false);
				return;
			}
			Jump(character, definition, updateContext.DeltaTime, jumpTimer, definition.MaximumJumpTimeSeconds);
			if (!character.UpdateJumpOnRailTracker(updateContext.DeltaTime))
			{
				character.Tracker.UpdateOffTracker(updateContext.DeltaTime);
			}
			if (!character.ActiveTracking.IsOnGround())
			{
				character.Storage.SetValue(ActorFSMKeys.JumpingExitedGround, true);
			}
		}

		// 060015b3 (1.10.1 RVA 0xE74C20, shared generic)
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnLeave(user, action);
			Character character = user.GetAs<Character>();
			TAbility ability = GetAbility(character);
			if (m_ballToBallAnimationBoolId != 0)
			{
				character.Animator.SetAnimation(m_ballToBallAnimationBoolId, false);
			}
			ability.EndJump();
			character.EndJumpImpulses();
			character.EndAirActiveImpulse();
			character.Storage.SetValue(ActorFSMKeys.JumpElapsedTime, 0f);
			character.Storage.SetValue(ActorFSMKeys.IsJumping, false);
			character.Storage.RemoveValue<Vector3>(ActorFSMKeys.JumpDirection);
			character.Storage.RemoveValue<bool>(ActorFSMKeys.JumpingExitedGround);
			character.Storage.RemoveValue<bool>(ActorFSMKeys.JumpDecelerationDisabled);
			character.Storage.RemoveValue<bool>(ActorFSMKeys.JumpFaceVelocityDirection);
		}

		// 060015b4 (1.10.1 RVA 0xE75110, shared generic); a negative jump timer applies nothing.
		private void Jump(Character character, TAbilityDef abilityDef, float deltaTime, float jumpTimer, float maxJumpTime)
		{
			if (jumpTimer < 0f)
			{
				return;
			}
			Vector3 jumpDirection = character.Storage.GetValue(ActorFSMKeys.JumpDirection, default(Vector3), true);
			float jumpPower = abilityDef.JumpPowerCurve.Evaluate(jumpTimer / maxJumpTime) * abilityDef.JumpPower;
			character.SetWorldVelocity(character.WorldVelocity + jumpDirection * (jumpPower * deltaTime));
		}

		// 060015b5 (1.10.1 RVA 0xE752D0, shared generic)
		public override void DebugInfo(Actor actor, StringBuilder stringInfoBuilder)
		{
			base.DebugInfo(actor, stringInfoBuilder);
			Character character = actor as Character;
			if (character == null)
			{
				return;
			}
			character.DebugGetJumpOnRailInfo(stringInfoBuilder);
		}
	}
}
