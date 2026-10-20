using System.Text;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200038d; bodies translated from the 1.10.1 x86-64 binary (shared generic bodies at
	// 0xE76440..0xE78910). The character rides the rail its tracker follows: it is attached at the rail speed, steered
	// along the tracker rotation, and on leaving either keeps the rail's jump magnetism data or is detached with an air
	// controls lock.
	[GraphNodeDefaultName("RailMovement")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[GraphNodeMenuFormat("Character/{0}")]
	public abstract class CharacterState_RailMovement<AbilityType, AbilityDefType> : CharacterState_Rail<AbilityType>
		where AbilityType : CharacterAbility_MovementRail<AbilityDefType>
		where AbilityDefType : CharacterAbilityDefinition_MovementRail
	{
		// 060015e2 (1.10.1 RVA 0xE76440, shared generic)
		protected CharacterState_RailMovement(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 060015e3 (1.10.1 RVA 0xE76470, shared generic). The character attaches at its current speed (the full speed
		// when a rail transfer carries the vertical velocity, otherwise the horizontal one) but not below the attach
		// speed, along the tracker direction.
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			Character character = user.GetAs<Character>();
			AbilityType ability = GetAbility(character);
			AbilityDefType definition = ability.Definition;
			character.SetTrackingType(CharacterTrackingType.SurfaceTracker);
			character.Tracker.OnTeleport(character.WorldPosition);
			character.Tracker.OnSurfaceChanged += OnSurfaceChanged;
			SurfaceLocation trackerLocation = character.Tracker.TrackerLocation;
			OnSurfaceChanged(character, trackerLocation, trackerLocation);
			if (ability.CanTurnAround)
			{
				float totalFixedTime = character.GetTotalFixedTime();
				if (totalFixedTime > character.Storage.GetValue(ActorFSMKeys.AirControlsLockTimeEnd, 0f, true))
				{
					character.DetectTurnAroundFromVelocity();
					character.DetectTurnAroundFromInput(-definition.TurnAroundInputThreshold);
				}
			}
			bool transferYVelocity = character.Storage.GetValue(ActorFSMKeys.RailTransferYVelocity, false, true);
			float speed = transferYVelocity ? character.WorldVelocityMagnitude : character.LocalVelocity.xz().magnitude;
			speed = Mathf.Max(speed, ability.Definition.AttachSpeedMin);
			character.SetWorldVelocity(character.Tracker.TrackerRotation * Vector3.forward * speed);
			if (transferYVelocity)
			{
				character.Storage.SetValue(ActorFSMKeys.RailTransferYVelocity, false);
			}
			ability.ClearJump();
			ability.SetActive(true);
			character.Storage.SetValue(ActorFSMKeys.StateRemainTimer, definition.StateTimeMin);
			UpdateRemainTimer(character, ability, false, 0f);
		}

		// 060015e4 (1.10.1 RVA 0xE76C40, shared generic)
		private void OnSurfaceChanged(Character character, SurfaceLocation fromLocation, SurfaceLocation toLocation)
		{
			GetAbility(character).OnSurfaceChanged(toLocation.m_surface);
			bool railExists = character.TrackManager.TrackableRailExists(toLocation.m_surface);
			character.Storage.SetValue(ActorFSMKeys.RailIsValid, railExists);
		}

		// 060015e5 (1.10.1 RVA 0xE76DA0, shared generic). The character counts as on the rail while its tracker and the
		// rail are valid, the ability is not detached and no jump off the rail is under way.
		protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			base.DoUpdate(user, updateContext);
			Character character = user.GetAs<Character>();
			AbilityType ability = GetAbility(character);
			float deltaTime = updateContext.DeltaTime;
			character.Storage.SetValue(ActorFSMKeys.JumpOnRailTracker, ability.JumpOnRailTracker);
			bool onRail = character.Storage.GetValue(ActorFSMKeys.RailIsValid, false, true)
				&& character.Tracker.LookAheadIsOnTracker()
				&& !ability.Detach
				&& !ability.JumpOnRailTracker;
			character.Storage.SetValue(ActorFSMKeys.RailActive, onRail);
			if (!onRail)
			{
				return;
			}
			if (character.Storage.GetValue(ActorFSMKeys.RailTurnAroundInProgress, false, true))
			{
				ability.UpdateTurnAround(deltaTime);
			}
			else
			{
				UpdateVelocity(character, ability, deltaTime);
			}
			OrientateToGround(character);
			ProcessMovement(character, ability.OrientateToWorldUp, deltaTime);
			if (ability.TurnAround)
			{
				character.TurnAround(ability.Definition.AttachSpeedMin);
			}
			UpdateRemainTimer(character, ability, true, deltaTime);
		}

		// 060015e6 (1.10.1 RVA 0xE77370, shared generic). Leaving by a jump hands the rail's jump magnetism to the jump
		// state, and snaps the character to the end of the rail when it is within the snap distance. Leaving any other
		// way locks the air controls briefly and buffers a ledge grab from the rail.
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnLeave(user, action);
			Character character = user.GetAs<Character>();
			AbilityType ability = GetAbility(character);
			CharacterTracker tracker = character.Tracker;
			if (ability.JumpOnRailTracker)
			{
				character.Storage.SetValue(ActorFSMKeys.JumpOnRailCanTurnAround, ability.CanTurnAround);
				character.Storage.SetValue(ActorFSMKeys.JumpOnRailMagnetism, ability.Definition.JumpMagnetism);
				character.Storage.SetValue(ActorFSMKeys.JumpOnRailDistanceMax, ability.Definition.JumpDistanceMax);
				character.Storage.SetValue(ActorFSMKeys.JumpOnRailAngleCosineMax, ability.Definition.JumpOnRailAngleCosineMax);
				float jumpDistanceToSnapToExit = ability.Definition.JumpDistanceToSnapToExit;
				float distanceFromEnd = tracker.TrackerDistanceFromEnd;
				if (jumpDistanceToSnapToExit > distanceFromEnd)
				{
					tracker.MoveTrackerByDistance(distanceFromEnd);
					character.SetWorldPosition(tracker.TrackerPosition);
					character.SetWorldRotation(tracker.TrackerRotation);
					character.SetWorldVelocity(character.WorldVelocityMagnitude * character.ForwardDirection);
				}
				character.Storage.RemoveValue<bool>(ActorFSMKeys.AirLedgeBufferFromRail);
			}
			else
			{
				character.SetAirControlsLockTime(character.Constants.RailDetachAirControlsLockTime);
				character.Storage.SetValue(ActorFSMKeys.AirLedgeBufferFromRail, true);
			}
			character.Storage.RemoveValue<bool>(ActorFSMKeys.RailIsValid);
			character.Storage.RemoveValue<float>(ActorFSMKeys.StateRemainTimer);
			CharacterAbilityDefinition exitDefinition = ability.GetExitAbilityDefinitionOverride();
			if (exitDefinition != null)
			{
				exitDefinition.CalculateCachedValues();
				character.Storage.SetValue(ActorFSMKeys.AbilityDefinitionOverride, exitDefinition);
			}
			ability.OnSurfaceChanged(null);
			tracker.OnSurfaceChanged -= OnSurfaceChanged;
			character.DetectTracking();
			ability.SetActive(false);
		}

		// 060015e7 (1.10.1 RVA 0xE77D90, shared generic)
		private void UpdateVelocity(Character character, AbilityType ability, float deltaTime)
		{
			float speed = character.LocalVelocity.xz().magnitude;
			Vector3 direction = character.Tracker.TrackerRotation * Vector3.forward;
			ability.UpdateForwardSpeed(ref speed, ref direction, deltaTime);
			character.SetWorldVelocity(direction * speed);
		}

		// 060015e8 (1.10.1 RVA 0xE77F00, shared generic)
		private void ProcessMovement(Character character, bool orientateToWorldUp, float deltaTime)
		{
			character.Tracker.ApplyVerticalVelocity(0f);
			character.Tracker.UpdateOnTrackerSpline(deltaTime, orientateToWorldUp);
			float turnAngle = 0f;
			CharacterMovementUtilities.UpdateStickyControls(character, ref turnAngle);
		}

		// 060015e9 (1.10.1 RVA 0xE77F60, shared generic); a character that is practically stationary on the plane is
		// left as it is.
		private void OrientateToGround(Character character)
		{
			if (character.LocalVelocity.xz().sqrMagnitude > 0.0001f)
			{
				character.OrientateToPlane(character.Tracker.TrackerRotation * Vector3.up, true);
			}
		}

		// 060015ea (1.10.1 RVA 0xE78010, shared generic)
		protected virtual bool CanQueueActions()
		{
			return true;
		}

		// 060015eb (1.10.1 RVA 0xE78020, shared generic). Without the four jump and boost abilities nothing is queued.
		// While time remains and CanQueueActions(), the character's queued jump and boost are handed to the abilities
		// on every call; once the time has run out they are handed over a last time, and when either is still queued
		// the queue and the air buffers are reset.
		private void UpdateRemainTimer(Character character, AbilityType railAbility, bool checkRemain, float deltaTime)
		{
			float remain = character.Storage.GetValue(ActorFSMKeys.StateRemainTimer, 0f, true);
			if (checkRemain && remain == 0f)
			{
				return;
			}
			character.TryGetAbility(ActorAbilityType.Character_Jump, out CharacterAbility_MovementJump jumpAbility);
			character.TryGetAbility(ActorAbilityType.Character_JumpOneShot, out CharacterAbility_MovementJumpOneShot jumpAbilityOneShot);
			character.TryGetAbility(ActorAbilityType.Character_MovementRailBoost, out CharacterAbility_MovementRailBoost boostAbility);
			character.TryGetAbility(ActorAbilityType.Character_MovementRailBoostOneShot, out CharacterAbility_MovementRailBoostOneShot boostAbilityOneShot);
			if (jumpAbility == null && jumpAbilityOneShot == null && boostAbility == null && boostAbilityOneShot == null)
			{
				return;
			}
			if (CanQueueActions())
			{
				ProcessQueuedJump(character, railAbility, jumpAbility, jumpAbilityOneShot);
				ProcessQueuedBoost(character, boostAbility, boostAbilityOneShot);
			}
			remain -= deltaTime;
			character.Storage.SetValue(ActorFSMKeys.StateRemainTimer, Mathf.Max(remain, 0f));
			if (remain > 0f)
			{
				return;
			}
			CharacterAbility_MovementAir airAbility = character.GetAbility<CharacterAbility_MovementAir>(ActorAbilityType.Character_MovementAir);
			if (airAbility == null)
			{
				return;
			}
			ProcessQueuedJump(character, railAbility, jumpAbility, jumpAbilityOneShot);
			ProcessQueuedBoost(character, boostAbility, boostAbilityOneShot);
			CharacterQueuedActions queuedActions = character.QueuedActions;
			if (queuedActions.Jump || queuedActions.Boost)
			{
				queuedActions.ResetBuffers();
				airAbility.ResetBuffers(false);
			}
		}

		// 060015ec (1.10.1 RVA 0xE784F0, shared generic); the queued jump goes to the one-shot ability while it is inside
		// its trigger window, and otherwise to the plain jump ability, and then leaves the rail.
		private static void ProcessQueuedJump(Character character, AbilityType railAbility, CharacterAbility_MovementJump jumpAbility, CharacterAbility_MovementJumpOneShot jumpAbilityOneShot)
		{
			CharacterQueuedActions queuedActions = character.QueuedActions;
			if (!queuedActions.Jump)
			{
				return;
			}
			if (jumpAbilityOneShot != null && jumpAbilityOneShot.CanTriggerBefore(queuedActions.JumpTimeElapsed))
			{
				jumpAbilityOneShot.QueueJump(true);
				QueueJump(character, railAbility);
			}
			else if (jumpAbility != null)
			{
				jumpAbility.QueueJump(true);
				QueueJump(character, railAbility);
			}
		}

		// 060015ed (1.10.1 RVA 0xE785D0, shared generic)
		private static void QueueJump(Character character, AbilityType railAbility)
		{
			railAbility.QueueJump();
			character.Storage.SetValue(ActorFSMKeys.RailActive, false);
			character.Storage.SetValue(ActorFSMKeys.JumpOnRailTracker, true);
		}

		// 060015ee (1.10.1 RVA 0xE787A0, shared generic)
		private static void ProcessQueuedBoost(Character character, CharacterAbility_MovementRailBoost boostAbility, CharacterAbility_MovementRailBoostOneShot boostAbilityOneShot)
		{
			CharacterQueuedActions queuedActions = character.QueuedActions;
			if (!queuedActions.Boost)
			{
				return;
			}
			if (boostAbilityOneShot != null && boostAbilityOneShot.CanTriggerBefore(queuedActions.BoostTimeElapsed))
			{
				boostAbilityOneShot.QueueBoost();
			}
			else if (boostAbility != null)
			{
				boostAbility.QueueBoost();
			}
		}

		// 060015ef (1.10.1 RVA 0xE78840, shared generic)
		public override void DebugInfo(Actor actor, StringBuilder stringInfoBuilder)
		{
			base.DebugInfo(actor, stringInfoBuilder);
			stringInfoBuilder.AppendLine(string.Format("SlopeAngle = {0:0.##}", GetAbility(actor).SlopeAngle));
		}
	}
}
