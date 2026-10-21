using Hardlight;
using System.Text;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000367; bodies translated from the 1.10.1 x86-64 binary (shared generic
	// bodies at 0xE6FA00..0xE73190). Ground movement: the character steers along the surface through
	// its active tracking, grips turns against slopes and brakes ahead of ledges.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Character/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	public abstract class CharacterState_Ground<TAbilityDefType, TAbilityType> : CharacterState_Ability<TAbilityType>
		where TAbilityDefType : CharacterAbilityDefinition_MovementGround
		where TAbilityType : CharacterAbility_MovementGround<TAbilityDefType>
	{
		private const float InContactWithGroundThreshold = 0.01f;

		// 0600155a (1.10.1 RVA 0xE6FA00)
		protected CharacterState_Ground(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 0600155b (1.10.1 RVA 0xE6FA30)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			Character character = user.GetAs<Character>();
			TAbilityType ability = GetAbility(character);
			TAbilityDefType definition = ability.Definition;
			if (ability.UpdateEnabled)
			{
				UpdateForSlope(character, ability);
				character.Storage.SetValue(ActorFSMKeys.StateRemainTimer, definition.StateTimeMin);
			}
			else
			{
				character.SetWorldVelocity(Vector3.zero);
			}
			character.UpdateTrackerOrientation();
			character.Storage.RemoveValue<float>(ActorFSMKeys.HomingAttackActionTimestamp);
		}

		// 0600155c (1.10.1 RVA 0xE6FD20)
		protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			base.DoUpdate(user, updateContext);
			Character character = user.GetAs<Character>();
			TAbilityType ability = GetAbility(character);
			TAbilityDefType definition = ability.Definition;
			float deltaTime = updateContext.DeltaTime;
			SetCameraProxyTargetSettings(character, ability);
			if (ability.UpdateEnabled)
			{
				UpdateForLanding(character, definition.LandingTime);
				UpdateForSlope(character, ability);
				character.ActiveTracking.GetSteeringInput(definition, deltaTime, out Vector3 inputForward, out float inputTurn);
				if (UpdateTurnGrip(character, ability, deltaTime, ref inputForward, ref inputTurn))
				{
					ProcessTurningMovement(character, ability, inputForward, inputTurn, deltaTime);
				}
			}
			OrientateToGround(character);
			if (ability.UpdateEnabled)
			{
				character.ActiveTracking.ProcessMovement(definition, deltaTime);
				if (ability.TurnAround)
				{
					character.TurnAround();
				}
				else
				{
					character.UpdateTrackerOrientation();
				}
			}
			character.Collider.StickToCollider(deltaTime);
			UpdateRemainTimer(character, deltaTime);
			UpdateLedgeBraking(character, definition.LedgeBrakeParameters);
		}

		// 0600155d (1.10.1 RVA 0xE6FF90); just after landing, the velocity is held to the recorded
		// landing speed along the current direction of travel.
		private void UpdateForLanding(Character character, float landingTimeMin)
		{
			float landingTime = character.Storage.GetValue(ActorFSMKeys.GroundLandingTime, 0f, false);
			if (character.GetTotalFixedTime() - landingTime > landingTimeMin)
			{
				return;
			}
			float landingSpeed = character.Storage.GetValue(ActorFSMKeys.GroundLandingSpeed, 0f, false);
			if (landingSpeed == 0f)
			{
				return;
			}
			if (character.WorldVelocityMagnitude > landingSpeed)
			{
				return;
			}
			character.SetWorldVelocity(character.WorldVelocityNormalised * landingSpeed);
		}

		// 0600155e (1.10.1 RVA 0xE701D0); on a slope between the stationary and orientation limits,
		// a character slower than the minimum input speed is turned along the slope and given that speed.
		private void UpdateForSlope(Character character, TAbilityType ability)
		{
			GlobalConstantDefinition_Character constants = character.Constants;
			float movementInputSpeedMin = constants.MovementInputSpeedMin;
			float stationarySlopeAngleMax = constants.MovementStationarySlopeAngleMax;
			float orientateToSlopeAngleMax = constants.MovementOrientateToSlopeAngleMax;
			TAbilityDefType definition = ability.Definition;
			if (definition.OverrideSlopeAngleMaxConstants)
			{
				stationarySlopeAngleMax = definition.MovementStationarySlopeAngleMaxOverride;
				orientateToSlopeAngleMax = definition.MovementOrientateToSlopeAngleMaxOverride;
			}
			bool onSlope = stationarySlopeAngleMax < ability.SlopeAngle && ability.SlopeAngle < orientateToSlopeAngleMax;
			float gripRestoreTimer = character.Storage.GetValue(ActorFSMKeys.GroundGripRestoreTimer, 0f, true);
			if (!onSlope || !(movementInputSpeedMin > character.WorldVelocityMagnitude) || gripRestoreTimer != 0f || character.HasConstantDirectionModifier)
			{
				return;
			}
			Vector3 up = character.UpDirection;
			float gravityDot = Vector3.Dot(character.GravityNormalised, up);
			Vector3 lookDirection = (up - character.GravityNormalised * gravityDot).normalized;
			character.OrientateToPlaneWithLookDirection(character.UpDirection, lookDirection);
			character.SetWorldVelocity(character.WorldRotation * (Vector3.forward * movementInputSpeedMin));
			character.Storage.SetValue(ActorFSMKeys.GroundGripRestoreTimer, character.Constants.GripRestoreTime);
		}

		// 0600155f (1.10.1 RVA 0xE70670)
		protected virtual bool CanQueueActions()
		{
			return true;
		}

		// 06001560 (1.10.1 RVA 0xE70680); native code skips everything when none of the four abilities
		// exists. While time remains and CanQueueActions(), each ability's current jump/boost request is
		// rerouted between the plain and one-shot abilities. Once the time runs out, the character's
		// queued jump is handed over, or failing that its queued boost, and on success the queue and
		// air buffers are reset.
		protected override void UpdateRemainTimer(Character character, float deltaTime)
		{
			float remain = character.Storage.GetValue(ActorFSMKeys.StateRemainTimer, 0f, true);
			if (remain == 0f)
			{
				return;
			}
			character.TryGetAbility(ActorAbilityType.Character_Jump, out CharacterAbility_MovementJump jumpAbility);
			character.TryGetAbility(ActorAbilityType.Character_JumpOneShot, out CharacterAbility_MovementJumpOneShot jumpAbilityOneShot);
			character.TryGetAbility(ActorAbilityType.Character_MovementGroundBoost, out CharacterAbility_MovementGroundBoost boostAbility);
			character.TryGetAbility(ActorAbilityType.Character_MovementGroundBoostOneShot, out CharacterAbility_MovementGroundBoostOneShot boostAbilityOneShot);
			if (jumpAbility == null && jumpAbilityOneShot == null && boostAbility == null && boostAbilityOneShot == null)
			{
				return;
			}
			if (CanQueueActions())
			{
				bool jumpTriggered = false;
				float jumpTimeElapsed = 0f;
				if (jumpAbility != null)
				{
					jumpTriggered = jumpAbility.Jump;
					jumpTimeElapsed = jumpAbility.TimeElapsed;
				}
				bool boostTriggered = false;
				float boostTimeElapsed = 0f;
				if (boostAbility != null)
				{
					boostTriggered = boostAbility.IsTriggered;
					boostTimeElapsed = boostAbility.TimeElapsed;
				}
				ProcessQueuedJump(jumpTriggered, jumpTimeElapsed, jumpAbility, jumpAbilityOneShot);
				ProcessQueuedBoost(boostTriggered, boostTimeElapsed, boostAbility, boostAbilityOneShot);
			}
			remain -= deltaTime;
			character.Storage.SetValue(ActorFSMKeys.StateRemainTimer, Mathf.Max(remain, 0f));
			if (remain > 0f)
			{
				return;
			}
			CharacterQueuedActions queuedActions = character.QueuedActions;
			if (ProcessQueuedJump(queuedActions.Jump, queuedActions.JumpTimeElapsed, jumpAbility, jumpAbilityOneShot)
				|| ProcessQueuedBoost(queuedActions.Boost, queuedActions.BoostTimeElapsed, boostAbility, boostAbilityOneShot))
			{
				queuedActions.ResetBuffers();
				character.GetAbility<CharacterAbility_MovementAir>(ActorAbilityType.Character_MovementAir)?.ResetBuffers(false);
			}
		}

		// 06001561 (1.10.1 RVA 0xE70CD0); a queued jump goes to the one-shot ability while it is inside its
		// trigger window, and otherwise to the plain jump ability.
		private static bool ProcessQueuedJump(bool queuedJump, float queuedJumpTimeElapsed, CharacterAbility_MovementJump jumpAbility, CharacterAbility_MovementJumpOneShot jumpAbilityOneShot)
		{
			if (!queuedJump)
			{
				return false;
			}
			if (jumpAbilityOneShot != null && jumpAbilityOneShot.CanTriggerBefore(queuedJumpTimeElapsed))
			{
				jumpAbilityOneShot.QueueJump(true);
				jumpAbility?.QueueJump(false);
				return true;
			}
			if (jumpAbility != null)
			{
				jumpAbilityOneShot?.QueueJump(false);
				jumpAbility.QueueJump(true);
				return true;
			}
			return false;
		}

		// 06001562 (1.10.1 RVA 0xE70D70)
		private static bool ProcessQueuedBoost(bool queueBoost, float queuedBoostTimeElapsed, CharacterAbility_MovementGroundBoost boostAbility, CharacterAbility_MovementGroundBoostOneShot boostAbilityOneShot)
		{
			if (!queueBoost)
			{
				return false;
			}
			if (boostAbilityOneShot != null && boostAbilityOneShot.CanTriggerBefore(queuedBoostTimeElapsed))
			{
				boostAbilityOneShot.QueueBoost(true);
				boostAbility?.QueueBoost(false);
				return true;
			}
			if (boostAbility != null)
			{
				boostAbilityOneShot?.QueueBoost(false);
				boostAbility.QueueBoost(true);
				return true;
			}
			return false;
		}

		// 06001563 (1.10.1 RVA 0xE70E20)
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnLeave(user, action);
			Character character = user.GetAs<Character>();
			character.Storage.SetValue(ActorFSMKeys.GroundCameraSettingsSet, false);
			character.Storage.RemoveValue<float>(ActorFSMKeys.StateRemainTimer);
			character.SetConnectionTrackingValues();
			character.Storage.RemoveValue<float>(ActorFSMKeys.GroundLandingSpeed);
			character.Storage.RemoveValue<float>(ActorFSMKeys.GroundLandingTime);
		}

		// 06001564 (1.10.1 RVA 0xE71170); the horizontal velocity is steered by the turning utilities
		// and accelerated along the resulting direction, keeping the vertical velocity.
		private void ProcessTurningMovement(Character character, TAbilityType ability, Vector3 inputForward, float inputTurn, float deltaTime)
		{
			TAbilityDefType definition = ability.Definition;
			Vector3 heading = MaintainHeading(ability) ? CachedHeading(character) : character.LocalVelocity;
			float verticalVelocity = heading.y;
			Vector3 planeVelocity = new Vector3(heading.x, 0f, heading.z);
			float forwardSpeed = Mathf.Sqrt(heading.x * heading.x + heading.z * heading.z);
			CharacterTraits.TurnTraits turnTraits = character.Collider.HasLinkedCollisionData && character.Collider.LinkedCollisionData.HasMovementOverride
				? character.Collider.LinkedCollisionData.MovementOverride.TurnTraits
				: definition.Turn;
			TurningMovementInput turningMovementInput = new TurningMovementInput(character, inputForward, inputTurn, forwardSpeed, turnTraits, deltaTime);
			CharacterMovementUtilities.ProcessTurningMovement(turningMovementInput, out Vector3 forwardVelocity, out Vector3 tangentVelocity, ref planeVelocity, out Vector3 inputDirection, out float effectiveInputMagnitude, out Quaternion inputRotation);
			character.Storage.SetValue(ActorFSMKeys.GroundInputDirection, inputDirection);
			ability.UpdateForwardSpeed(ref forwardSpeed, ref inputDirection, deltaTime, 0f, effectiveInputMagnitude, turnTraits.AccelerationMultiplier, true);
			forwardVelocity = inputDirection * forwardSpeed;
			planeVelocity = tangentVelocity + forwardVelocity;
			character.SetLocalVelocity(new Vector3(0f, verticalVelocity, 0f) + planeVelocity);
		}

		// 06001565 (1.10.1 RVA 0xE71590); returns whether turning is gripped. While the grip is being
		// restored it is zero. Otherwise the grip comes from the slope-direction curves: a positive grip
		// steers the turn input toward the surface, and a grip of zero or less starts the restore time
		// (0xE71CF6 jbe).
		private bool UpdateTurnGrip(Character character, TAbilityType ability, float deltaTime, ref Vector3 inputForward, ref float inputTurn)
		{
			float boostRestoreTimer = character.Storage.GetValue(ActorFSMKeys.GroundBoostRestoreTimer, 0f, true);
			if (boostRestoreTimer > 0f)
			{
				character.Storage.SetValue(ActorFSMKeys.GroundBoostRestoreTimer, Mathf.Max(boostRestoreTimer - deltaTime, 0f));
			}
			// Native code reads and discards the grip value here, which stores the default on first read.
			character.Storage.GetValue(ActorFSMKeys.GroundGripValue, 0f, true);
			float gripRestoreTimer = character.Storage.GetValue(ActorFSMKeys.GroundGripRestoreTimer, 0f, true);
			float gripValue;
			if (gripRestoreTimer > 0f)
			{
				character.Storage.SetValue(ActorFSMKeys.GroundGripRestoreTimer, Mathf.Max(gripRestoreTimer - deltaTime, 0f));
				gripValue = 0f;
			}
			else if (ability.GripAgainstGravityMultiplierCurve == null || ability.GripAgainstGravityMultiplierCurve.length < 2 || character.HasConstantDirectionModifier)
			{
				character.Storage.SetValue(ActorFSMKeys.GroundGripTime, 0f);
				gripValue = 1f;
			}
			else
			{
				float gripTime = character.Storage.AdjustValue(ActorFSMKeys.GroundGripTime, deltaTime, 0f);
				Vector3 worldUp = character.WorldUp;
				Vector3 cameraForward = character.CameraForwardOnCharacterPlane;
				Quaternion turnRotation = Quaternion.AngleAxis(inputTurn, character.UpDirection);
				Vector3 intendedDirection = turnRotation * cameraForward;
				AnimationCurve gripCurve = Vector3.Dot(worldUp, intendedDirection) < 0f ? ability.GripWithGravityMultiplierCurve : ability.GripAgainstGravityMultiplierCurve;
				gripValue = gripCurve.Evaluate(gripTime);
				if (ability.GripSpeedMultiplierCurve != null && ability.GripSpeedMultiplierCurve.length > 0)
				{
					gripValue *= ability.GripSpeedMultiplierCurve.Evaluate(character.WorldVelocityMagnitude);
				}
				if (gripValue > 0f)
				{
					if (inputForward.sqrMagnitude > 0f)
					{
						Vector3 steerDirection = intendedDirection - worldUp * (2f * (1f - gripValue));
						inputTurn = Vector3.SignedAngle(cameraForward, steerDirection, character.UpDirection);
					}
				}
				else
				{
					character.Storage.SetValue(ActorFSMKeys.GroundGripRestoreTimer, character.Constants.GripRestoreTime);
				}
			}
			character.Storage.SetValue(ActorFSMKeys.GroundGripValue, gripValue);
			return gripValue > 0f;
		}

		// 06001566 (1.10.1 RVA 0xE71E60); the camera proxy settings are applied once per state, and
		// not while the ground hit is farther than the contact threshold.
		protected override void SetCameraProxyTargetSettings(Character character, TAbilityType ability)
		{
			if (character.Storage.GetValue(ActorFSMKeys.GroundCameraSettingsSet, false, true))
			{
				return;
			}
			RaycastColliderHit groundHit = character.Collider.GroundHit;
			if (groundHit.Hit && groundHit.HitDistance() >= InContactWithGroundThreshold)
			{
				return;
			}
			if (ability.DefinitionBase is CharacterAbilityDefinition definition)
			{
				CameraUtilities.SetCharacterCameraProxyTargetSettings(character, definition.CameraProxyTargetSettings);
			}
			character.Storage.SetValue(ActorFSMKeys.GroundCameraSettingsSet, true);
		}

		// 06001567 (1.10.1 RVA 0xE72140)
		protected virtual void OrientateToGround(Character character)
		{
			character.ActiveTracking.OrientateToGround();
		}

		// 06001568 (1.10.1 RVA 0xE72160)
		public override void DebugInfo(Actor actor, StringBuilder stringInfoBuilder)
		{
			base.DebugInfo(actor, stringInfoBuilder);
			TAbilityType ability = GetAbility(actor);
			float gripValue = actor.Storage.GetValue(ActorFSMKeys.GroundGripValue, 0f, true);
			CharacterAbilityDefinition.Motion gripMotion = actor.Storage.GetValue<CharacterAbilityDefinition.Motion>(ActorFSMKeys.GroundGripMotion, null, true);
			float clamp = 0f;
			float gripRestoreTimer = actor.Storage.GetValue(ActorFSMKeys.GroundGripRestoreTimer, 0f, true);
			if (gripMotion != null && gripMotion.ClampToSpeedMin)
			{
				clamp = gripMotion.SpeedMin;
			}
			stringInfoBuilder.AppendLine(string.Format("Grip = {0:0.##}, Clamp = {1:0.##}, SlopeAngle = {2:0.##}, GripTimer = {3:0.##}", gripValue, clamp, ability.SlopeAngle, gripRestoreTimer));
		}

		// 06001569 (1.10.1 RVA 0xE725E0); a moving character within reach of a detected ledge stops
		// when it is on the ledge, and otherwise loses the velocity along the ledge direction in
		// proportion to its closeness, unless the input deliberately points toward the ledge (intent
		// cosine above the speed-dependent limit).
		protected virtual void UpdateLedgeBraking(Character character, LedgeBrakeParameters ledgeBrakeParameters)
		{
			if (0.0001f >= character.WorldVelocityMagnitude)
			{
				return;
			}
			if (!DetectLedgeAhead(character, ledgeBrakeParameters))
			{
				return;
			}
			Vector3 ledgePosition = character.Storage.GetValue(ActorFSMKeys.GroundLedgePosition, default(Vector3), true);
			Vector3 toLedge = ledgePosition - character.WorldPosition;
			float distance = toLedge.magnitude;
			Vector3 ledgeDirection = distance < 0.0001f ? character.ForwardDirection : Vector3.ProjectOnPlane(toLedge, character.UpDirection).normalized;
			if (character.ControllerMovementMagnitude > 0.0001f)
			{
				Vector3 intendedForward = CharacterMovementUtilities.GetIntendedForward(character, character.ControllerMovement);
				float intentCosAngle = character.WorldVelocityMagnitude > ledgeBrakeParameters.MaximumVelocityThreshold ? ledgeBrakeParameters.IntentCosAngleAboveVelocity : ledgeBrakeParameters.IntentCosAngleBelowVelocity;
				if (Vector3.Dot(intendedForward, ledgeDirection) > intentCosAngle)
				{
					return;
				}
			}
			if (character.BoostStamina.Active)
			{
				character.BoostStamina.ActivateCooldownFromImpact();
			}
			if (0.0001f > distance)
			{
				character.SetWorldVelocity(Vector3.zero);
				return;
			}
			float closeness = 1f - distance / ledgeBrakeParameters.RaycastDetectDistance;
			float brake = Vector3.Dot(character.WorldVelocity, ledgeDirection) * closeness;
			character.SetWorldVelocity(character.WorldVelocity - ledgeDirection * brake);
		}

		// 0600156a (1.10.1 RVA 0xE72AE0); casts down from ahead of the character, and then down and
		// forward from the character, to find a ledge. A steep hit ahead is a ledge and records nothing; a
		// walkable hit ahead records its point and is not a ledge; otherwise the down-forward ray decides
		// by a height compared against LedgeCosAngleLimit (as in the original, 0xE7305E), and a miss
		// records the character position as the ledge.
		private bool DetectLedgeAhead(Character character, LedgeBrakeParameters ledgeBrakeParameters)
		{
			Vector3 gravityNormalised = character.GravityNormalised;
			if (ledgeBrakeParameters.MaximumCosAngleToPlane > Vector3.Dot(-gravityNormalised, character.UpDirection))
			{
				return false;
			}
			float raycastDownDistance = ledgeBrakeParameters.RaycastDownDistance;
			Vector3 aheadOrigin = character.Bounds.center + character.ForwardDirection * ledgeBrakeParameters.RaycastDetectDistance + character.UpDirection * character.Bounds.extents.y;
			if (Physics.Raycast(aheadOrigin, gravityNormalised, out RaycastHit aheadHit, raycastDownDistance, character.TrackMask))
			{
				if (ledgeBrakeParameters.LedgeCosAngleLimit > Vector3.Dot(character.UpDirection, aheadHit.normal))
				{
					return true;
				}
				character.Storage.SetValue(ActorFSMKeys.GroundLedgePosition, aheadHit.point);
				return false;
			}
			Vector3 stopOrigin = character.Bounds.center + character.UpDirection * character.Bounds.extents.y;
			Vector3 downForward = (gravityNormalised + character.ForwardDirection * ledgeBrakeParameters.RaycastStopDistance).normalized;
			if (Physics.Raycast(stopOrigin, downForward, out RaycastHit stopHit, raycastDownDistance, character.TrackMask))
			{
				return Vector3.Dot(character.WorldPosition - stopHit.point, character.UpDirection) > ledgeBrakeParameters.LedgeCosAngleLimit;
			}
			character.Storage.SetValue(ActorFSMKeys.GroundLedgePosition, character.WorldPosition);
			return true;
		}
	}
}
