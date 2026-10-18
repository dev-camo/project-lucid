using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000370; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Character/{0}")]
	[GraphNodeDefaultName("HalfPipeAir")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterState_HalfPipeAir : CharacterState_HalfPipe<CharacterAbilityDefinition_HalfPipeAir, CharacterAbility_HalfPipeAir>
	{
		// Original nested 02000371.
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[FiniteStateMachineJsonCtorArgs]
		[Serializable]
		protected class HalfPipeAirJSONCtorArgs : AbilityJSONCtorArgs
		{
			public ActorAbilityType HalfPipeTransferAbilityType;

			// 0600158c (1.10.1 RVA 0x778D10)
			public HalfPipeAirJSONCtorArgs()
			{
			}
		}

		private readonly ActorAbilityType m_halfPipeTransferAbilityType;

		// 06001586 (1.10.1 RVA 0x7771A0)
		protected CharacterState_HalfPipeAir(FiniteStateMachine fsm, FSMIdentifier stateId, HalfPipeAirJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
			m_halfPipeTransferAbilityType = ctorArgs.HalfPipeTransferAbilityType;
		}

		// 06001587 (1.10.1 RVA 0x777210)
		public static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_HalfPipeAir(fsm, transitionId, JsonUtility.FromJson<HalfPipeAirJSONCtorArgs>(jsonCtorArgs));
		}

		// 06001588 (1.10.1 RVA 0x7772D0)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			Character character = user.GetAs<Character>();
			CharacterAbilityDefinition_HalfPipeAir definition = GetAbility(character).Definition;
			Vector3 colliderNormal = character.Storage.GetValue(ActorFSMKeys.HalfPipeColliderNormal, Vector3.zero, true);
			ColliderTrackingUtilities.ProcessHalfPipeTrajectory(character, definition.Trajectory);
			ColliderTrackingUtilities.CalculateCharacterSpeedAlongPlane(character, colliderNormal, out Vector3 colliderForward, out float colliderSpeedForward);
			ColliderTrackingUtilities.ProjectCharacterToCollider(character, colliderForward, out Vector3 colliderPosition);
			character.Storage.SetValue(ActorFSMKeys.HalfPipeColliderActive, true);
			character.Storage.SetValue(ActorFSMKeys.HalfPipeColliderUpdate, false);
			character.Storage.SetValue(ActorFSMKeys.HalfPipeColliderPosition, colliderPosition);
			character.Storage.SetValue(ActorFSMKeys.HalfPipeColliderSpeedForward, colliderSpeedForward);
		}

		// 06001589 (1.10.1 RVA 0x777810); a failed raycast projection tries the transfer ability, a ground hit clears the collider flag.
		protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			base.DoUpdate(user, updateContext);
			Character character = user.GetAs<Character>();
			if (!character.Storage.GetValue(ActorFSMKeys.HalfPipeColliderActive, false, true))
			{
				return;
			}
			CharacterAbility_HalfPipeAir ability = GetAbility(character);
			Vector3 colliderPosition = character.Storage.GetValue(ActorFSMKeys.HalfPipeColliderPosition, Vector3.zero, true);
			Vector3 colliderNormal = character.Storage.GetValue(ActorFSMKeys.HalfPipeColliderNormal, Vector3.zero, true);
			float colliderSpeedForward = character.Storage.GetValue(ActorFSMKeys.HalfPipeColliderSpeedForward, 0f, true);
			if (character.Storage.GetValue(ActorFSMKeys.HalfPipeColliderUpdate, false, true))
			{
				float deltaTime = updateContext.DeltaTime;
				bool projected = ColliderTrackingUtilities.ProjectColliderRaycastLocation(character, colliderSpeedForward * deltaTime, ref colliderPosition, ref colliderNormal);
				character.Storage.SetValue(ActorFSMKeys.HalfPipeColliderActive, projected);
				if (!projected)
				{
					bool transferred = ActivateAbilityTransfer(character, ability, colliderPosition, colliderNormal, deltaTime);
					character.Storage.SetValue(ActorFSMKeys.HalfPipeTransferActive, transferred);
					return;
				}
				if (character.Collider.GroundHit.Hit)
				{
					character.Storage.SetValue(ActorFSMKeys.HalfPipeColliderActive, false);
					character.Storage.SetValue(ActorFSMKeys.AirProjectedBufferDisabled, true);
					return;
				}
			}
			else
			{
				character.Storage.SetValue(ActorFSMKeys.HalfPipeColliderUpdate, !character.Collider.GroundHit.Hit);
			}
			Vector3 colliderNormalised = colliderNormal.normalized;
			float planeDistance = Vector3.Dot(character.WorldPosition, colliderNormalised) - Vector3.Dot(colliderPosition, colliderNormalised);
			Vector3 position = character.WorldPosition - colliderNormalised * planeDistance + colliderNormal * ability.Definition.OffsetFromTracker;
			Vector3 velocity = Vector3.ProjectOnPlane(character.WorldVelocity, colliderNormal);
			character.SetWorldPosition(position);
			character.SetWorldVelocity(velocity);
			character.SetWorldRotation(Quaternion.LookRotation(velocity, colliderNormal));
			character.Storage.SetValue(ActorFSMKeys.HalfPipeColliderPosition, colliderPosition);
			character.Storage.SetValue(ActorFSMKeys.HalfPipeColliderNormal, colliderNormal);
		}

		// 0600158a (1.10.1 RVA 0x778850)
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnLeave(user, action);
			Character character = user.GetAs<Character>();
			character.Storage.RemoveValue<bool>(ActorFSMKeys.HalfPipeColliderActive);
			character.Storage.RemoveValue<bool>(ActorFSMKeys.HalfPipeColliderUpdate);
			character.Storage.RemoveValue<float>(ActorFSMKeys.HalfPipeColliderSpeedForward);
			character.Storage.SetValue(ActorFSMKeys.GroundLandingSpeed, character.WorldVelocityMagnitude);
			character.Storage.SetValue(ActorFSMKeys.GroundLandingTime, character.GetTotalFixedTime());
			character.Storage.SetValue(ActorFSMKeys.GroundBoostRestoreTimer, character.Constants.BoostRestoreTimeFromHalfPipe);
		}

		// 0600158b (1.10.1 RVA 0x778650); colliderPosition is not read by the native body.
		private bool ActivateAbilityTransfer(Character character, CharacterAbility_HalfPipeAir ability, Vector3 colliderPosition, Vector3 colliderNormal, float deltaTime)
		{
			CharacterAbility_HalfPipeTransfer transfer = null;
			if (character.TryGetAbility(m_halfPipeTransferAbilityType, out transfer))
			{
				ColliderTrackingUtilities.RaycastTransform raycastTransform = new ColliderTrackingUtilities.RaycastTransform
				{
					LayerMask = character.TrackMask,
					Position = character.WorldPosition,
					Direction = -colliderNormal,
					Up = Vector3.up
				};
				float speed = character.WorldVelocityMagnitude;
				Vector3 direction = character.WorldVelocity / speed;
				transfer.CacheTransform(ability.AirMotion, raycastTransform, direction, speed, character.Gravity);
				return transfer.ProjectToHalfPipe(deltaTime);
			}
			return false;
		}
	}
}
