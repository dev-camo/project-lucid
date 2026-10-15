using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000351; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Character/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterState_AirAbility : CharacterState_Ability<CharacterAbility_Air>
	{
		// 0600150d (1.10.1 RVA 0x772F00)
		protected CharacterState_AirAbility(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
			
		}

		// 0600150e (1.10.1 RVA 0x772F70)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_AirAbility(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}

		// 0600150f (1.10.1 RVA 0x773030)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			Character character = user.GetAs<Character>();
			CharacterAbility_Air ability = GetAbility(character);
			CharacterAbilityDefinition_Air definition = ability.Definition;
			Vector3 gravityNormalised = character.GravityNormalised;
			bool forwardFromVelocity = definition.MaintainOrientationFromVelocity && character.LocalVelocity.XZ().sqrMagnitude > 0.0001f;
			// Entry nudge clears walls and perpendicular gravity before orientation (0x77315E-0x7731ED).
			if (definition.NudgeCharacterUpOnEnter && Mathf.Abs(Vector3.Dot(character.UpDirection, gravityNormalised)) <= definition.NudgeCharacterUpOnEnterMaxDotProduct)
			{
				character.SetWorldPosition(character.WorldPosition + character.UpDirection * definition.NudgeCharacterUpOnEnterDistance);
			}
			OrientateToGravity(character, forwardFromVelocity ? character.WorldVelocityNormalised : character.ForwardDirection, gravityNormalised);
			if (definition.OrientateToInputOnEnter)
			{
				CharacterAbility_MovementAir movementAir = character.GetAbility<CharacterAbility_MovementAir>(ActorAbilityType.Character_MovementAir);
				if (movementAir != null)
				{
					ability.ApplyInputModifier();
					CharacterMovementUtilities.ProcessAirControl(character, movementAir, movementAir.Definition, false, 0f, true);
				}
			}
			UpdateCachedValues(character, ability, gravityNormalised);
			ability.VelocityStorage.StoreVelocity();
			character.TriggerBurstAttackAir(false);
		}

		// 06001510 (1.10.1 RVA 0x773590)
		protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			base.DoUpdate(user, updateContext);
			Character character = user.GetAs<Character>();
			CharacterAbility_Air ability = GetAbility(character);
			CharacterAbilityDefinition_Air definition = ability.Definition;
			float deltaTime = updateContext.DeltaTime;
			Vector3 startForward = character.ForwardDirection;
			ability.UpdateVelocity(deltaTime);
			Vector3 blendGravity = character.Storage.GetValue(ActorFSMKeys.AirAbilityBlendGravity, Vector3.zero, true);
			Vector3 gravityNormalised = character.GetGravityAt(character.WorldPosition).normalized;
			if ((blendGravity - gravityNormalised).sqrMagnitude >= 1e-10f)
			{
				EnableGravity(character, true);
				OrientateToGravity(character, character.ForwardDirection, gravityNormalised);
				UpdateCachedValues(character, ability, gravityNormalised);
			}
			Vector2 controllerMovement = character.ControllerMovement;
			float inputTurn = Vector2.SignedAngle(controllerMovement, Vector2.up);
			Vector3 worldVelocity = character.WorldVelocity;
			Vector3 planeVelocity = character.LocalVelocity.XZ();
			float inputMagnitude = controllerMovement.magnitude;
			float forwardSpeed = planeVelocity.magnitude;
			TurningMovementInput input = new TurningMovementInput(character, new Vector3(0f, 0f, inputMagnitude), inputTurn, forwardSpeed, definition.Turn, deltaTime);
			if (ability.OrientateHeadingToPlane && ability.OrientateColliderHit)
			{
				CharacterMovementUtilities.ProcessTurningMovement(input, out Vector3 forwardVelocity, out Vector3 _, ref planeVelocity, out Vector3 _, out float _, out Quaternion _);
				if (forwardVelocity.sqrMagnitude > 0.0001f)
				{
					Vector3 heading = (character.WorldRotation * forwardVelocity).normalized;
					character.SetWorldRotation(Quaternion.LookRotation(heading, character.UpDirection));
				}
			}
			else
			{
				Vector3 forwardPlane = Vector3.forward * forwardSpeed;
				CharacterMovementUtilities.ProcessTurningMovement(input, out Vector3 forwardVelocity, out Vector3 _, ref forwardPlane, out Vector3 _, out float _, out Quaternion _);
				float sqrSpeed = forwardVelocity.sqrMagnitude;
				Vector3 heading;
				if (MathUtilities.WithinTolerance(sqrSpeed, 0f, 0.0001f))
				{
					heading = character.ForwardDirection;
				}
				else
				{
					heading = character.WorldRotation * forwardVelocity / Mathf.Sqrt(sqrSpeed);
				}
				OrientateToGravity(character, heading, gravityNormalised);
			}
			BlendToVelocity(character, ability, deltaTime);
			character.Tracker.UpdateOffTracker(deltaTime);
			UpdateAbilityInterrupt(character, ability, definition, worldVelocity, deltaTime);
			Vector3 currentForward = Vector3.ProjectOnPlane(character.ForwardDirection, character.UpDirection);
			Vector3 previousForward = Vector3.ProjectOnPlane(startForward, character.UpDirection);
			float turnAngle = Vector3.SignedAngle(previousForward, currentForward, character.UpDirection);
			UpdateAnimation(character, Mathf.Clamp(turnAngle / deltaTime / definition.Turn.BaseRateOfTurn, -1f, 1f));
		}

		// 06001511 (1.10.1 RVA 0x7749A0)
		protected override void OrientateToGravity(Actor actor, Vector3 forwardDirection, Vector3 gravityNormalised)
		{
			if (GetAbility(actor).Definition.OrientateToGravity)
			{
				base.OrientateToGravity(actor, forwardDirection, gravityNormalised);
			}
		}

		// 06001512 (1.10.1 RVA 0x7732F0)
		private void UpdateCachedValues(Character character, CharacterAbility_Air ability, Vector3 blendGravity)
		{
			character.Storage.SetValue(ActorFSMKeys.AirAbilityBlendVelocity, character.LocalVelocity);
			character.Storage.SetValue(ActorFSMKeys.AirAbilityBlendGravity, blendGravity);
			character.Storage.SetValue(ActorFSMKeys.AirAbilityActive, ability);
		}

		// 06001513 (1.10.1 RVA 0x774210)
		private void BlendToVelocity(Character character, CharacterAbility_Air ability, float deltaTime)
		{
			Vector3 targetVelocity = ability.Velocity;
			if (targetVelocity.sqrMagnitude == 0f)
			{
				character.SetWorldVelocity(Vector3.zero);
				return;
			}
			Vector3 velocity = targetVelocity;
			if ((targetVelocity - character.WorldVelocity).sqrMagnitude > 0.0001f)
			{
				Vector3 blendVelocity = character.Storage.GetValue(ActorFSMKeys.AirAbilityBlendVelocity, Vector3.zero, true);
				Vector3 worldBlend = character.WorldRotation * blendVelocity + character.Gravity * deltaTime;
				Vector3 localBlend = character.WorldToLocalRotation * worldBlend;
				character.Storage.SetValue(ActorFSMKeys.AirAbilityBlendVelocity, localBlend);
				Vector3 localTarget = character.WorldToLocalRotation * targetVelocity;
				Vector3 progress = ability.BlendProgress;
				Vector3 blended = new Vector3(
					BlendVelocityComponent(localBlend.x, localTarget.x, progress.x),
					BlendVelocityComponent(localBlend.y, localTarget.y, progress.y),
					BlendVelocityComponent(localBlend.z, localTarget.z, progress.z));
				velocity = character.WorldRotation * blended;
			}
			ability.SetWorldVelocity(velocity, deltaTime);
		}

		// 06001514 (1.10.1 RVA 0x774600)
		private void UpdateAbilityInterrupt(Character character, CharacterAbility_Air ability, CharacterAbilityDefinition_Air abilityDef, Vector3 worldVelocity, float deltaTime)
		{
			bool interrupt = false;
			CharacterCollider collider = character.Collider;
			if (collider.GetNonTrackCollisionContactCount() > 0)
			{
				float slope = Vector3.Dot(collider.GetNonTrackCollisionContactNormal(), character.UpDirection);
				if (abilityDef.DeactivateOnNonTrackSlopeCosineAngleMin > slope && slope > abilityDef.DeactivateOnNonTrackSlopeCosineAngleMax)
				{
					interrupt = true;
				}
			}
			if (!interrupt && 1f > abilityDef.DeactivateOnGravityCosineAngleChange)
			{
				if (abilityDef.DeactivateOnGravityCosineAngleChange > Vector3.Dot(character.GravityNormalised, character.GravityNormalisedLastFrame))
				{
					interrupt = true;
				}
			}
			if (!interrupt && abilityDef.DeactivateOnCollisionVelocityThreshold > 0f)
			{
				if (abilityDef.DeactivateOnCollisionVelocityThreshold > Mathf.Abs(Vector3.Dot(ability.Velocity.normalized, worldVelocity)))
				{
					interrupt = true;
				}
			}
			if (!interrupt)
			{
				character.Storage.SetValue(ActorFSMKeys.AirAbilityInterruptTimer, 0f);
				return;
			}
			float timer = character.Storage.AdjustValue(ActorFSMKeys.AirAbilityInterruptTimer, deltaTime, 0f);
			if (abilityDef.DeactivateTime > timer)
			{
				return;
			}
			ability.Interrupt();
		}

		// 06001515 (1.10.1 RVA 0x774A90)
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnLeave(user, action);
			Character character = user.GetAs<Character>();
			CharacterAbility_Air ability = GetAbility(character);
			CharacterAbilityDefinition_Air definition = ability.Definition;
			EnableGravity(character, true);
			Vector3 gravityNormalised = character.GetGravityAt(character.WorldPosition).normalized;
			OrientateToGravity(character, character.ForwardDirection, gravityNormalised);
			if (definition.DeactivateToZeroVelocityIfNoInput && !character.HasControllerMovement)
			{
				character.SetWorldVelocity(Vector3.zero);
			}
			ability.Interrupt();
			character.Storage.SetValue<CharacterAbility_Air>(ActorFSMKeys.AirAbilityActive, null);
		}

		// 06001516 (1.10.1 RVA 0x774A60)
		private float BlendVelocityComponent(float velocityStart, float velocityTarget, float blendProgress)
		{
			return velocityStart + (velocityTarget - velocityStart) * Mathf.Clamp01(blendProgress);
		}

		// 06001517 (1.10.1 RVA 0x774980)
		private void UpdateAnimation(Character character, float rotationSpeedRatio)
		{
			character.Animator.TrySetFromAnimationType(m_stateAnimationDefinition, ActorAnimationType.AirTilt, rotationSpeedRatio);
		}
	}
}
