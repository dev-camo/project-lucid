using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000394; bodies translated from the 1.10.1 x86-64 binary. The character rides the
	// transporter its tracker follows: it moves along the tracker direction, lies on the tracker plane with the
	// visual proxy turned flat, and on leaving is stood upright again at the transporter's exit rotation.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[GraphNodeMenuFormat("Character/{0}")]
	[GraphNodeDefaultName("TransporterMovement")]
	public class CharacterState_TransporterMovement : CharacterState_Ability<CharacterAbility_MovementTransporter>
	{
		// 06001604 (1.10.1 RVA 0x7808C0)
		protected CharacterState_TransporterMovement(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 06001605 (1.10.1 RVA 0x780930)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_TransporterMovement(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}

		// 06001606 (1.10.1 RVA 0x7809F0)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			Character character = user.GetAs<Character>();
			character.SetTrackingType(CharacterTrackingType.SurfaceTracker);
			character.Tracker.OnTeleport(character.WorldPosition);
			character.Tracker.OnSurfaceChanged += OnSurfaceChanged;
			SurfaceLocation trackerLocation = character.Tracker.TrackerLocation;
			OnSurfaceChanged(character, trackerLocation, trackerLocation);
		}

		// 06001607 (1.10.1 RVA 0x780D40); the transporter check precedes the ability notification.
		private void OnSurfaceChanged(Character character, SurfaceLocation fromLocation, SurfaceLocation toLocation)
		{
			bool transporterExists = character.TrackManager.TrackableTransporterExists(toLocation.m_surface);
			GetAbility(character).OnSurfaceChanged(toLocation.m_surface);
			character.Storage.SetValue(ActorFSMKeys.TransporterIsValid, transporterExists);
		}

		// 06001608 (1.10.1 RVA 0x780EA0). The character counts as on the transporter while its tracker surface is valid
		// and the tracker looks ahead onto it; only then is it moved.
		protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			base.DoUpdate(user, updateContext);
			Character character = user.GetAs<Character>();
			CharacterAbility_MovementTransporter ability = GetAbility(character);
			bool onTransporter = character.Storage.GetValue(ActorFSMKeys.TransporterIsValid, false, true) && character.Tracker.LookAheadIsOnTracker();
			character.Storage.SetValue(ActorFSMKeys.TransporterActive, onTransporter);
			if (!onTransporter)
			{
				return;
			}
			UpdateVelocity(character, ability, updateContext.DeltaTime);
			OrientateToTracker(character);
			OverrideProxy(character);
			character.Tracker.ApplyVerticalVelocity(0f);
			character.Tracker.UpdateOnTrackerSpline(updateContext.DeltaTime);
		}

		// 06001609 (1.10.1 RVA 0x7818E0)
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnLeave(user, action);
			Character character = user.GetAs<Character>();
			CharacterAbility_MovementTransporter ability = GetAbility(character);
			UpdateVelocity(character, ability, 0f);
			character.SetAirControlsLockTime(character.Constants.RailDetachAirControlsLockTime);
			character.Storage.RemoveValue<bool>(ActorFSMKeys.TransporterIsValid);
			character.Storage.RemoveValue<float>(ActorFSMKeys.StateRemainTimer);
			ability.OnSurfaceChanged(null);
			character.Tracker.OnSurfaceChanged -= OnSurfaceChanged;
			character.DetectTracking();
			ResetProxy(character);
			OrientateOnExit(character, ability);
		}

		// 0600160a (1.10.1 RVA 0x7812F0); deceleration applies unless the auto rail modifier is set.
		private void UpdateVelocity(Character character, CharacterAbility_MovementTransporter ability, float deltaTime)
		{
			float speed = character.LocalVelocity.xz().magnitude;
			Vector3 direction = character.Tracker.TrackerRotation * Vector3.forward;
			bool applyDeceleration = !character.GetModifierValue<bool>((int)GameplayModifierType.AutoRail);
			ability.UpdateForwardSpeed(ref speed, ref direction, deltaTime, 1f, 1f, null, applyDeceleration);
			character.SetWorldVelocity(direction * speed);
		}

		// 0600160b (1.10.1 RVA 0x7815D0); a character that is practically stationary on the plane is left as it is.
		private void OrientateToTracker(Character character)
		{
			if (character.LocalVelocity.xz().sqrMagnitude < 0.0001f)
			{
				return;
			}
			character.OrientateToPlane(character.Tracker.TrackerRotation * Vector3.up, true);
		}

		// 0600160c (1.10.1 RVA 0x781C20)
		private void OrientateOnExit(Character character, CharacterAbility_MovementTransporter ability)
		{
			character.OrientateToPlane(character.WorldUp, true);
			character.SetWorldRotation(character.WorldRotation * Quaternion.AngleAxis(ability.GetExitOrientation(), Vector3.up));
		}

		// 0600160d (1.10.1 RVA 0x7817B0)
		private void OverrideProxy(Character character)
		{
			character.VisualProxy.OverrideRotation(character.WorldRotation * Quaternion.AngleAxis(90f, Vector3.right));
		}

		// 0600160e (1.10.1 RVA 0x781C10)
		private void ResetProxy(Character character)
		{
			character.VisualProxy.StopOverridingRotation();
		}
	}
}
