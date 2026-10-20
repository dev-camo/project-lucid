using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000385; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("LightspeedDash")]
	[GraphNodeMenuFormat("Character/{0}")]
	public class CharacterState_LightspeedDash : CharacterState_Ability<CharacterAbility_LightspeedDash>
	{
		// 060015c6 (1.10.1 RVA 0x77BF70)
		protected CharacterState_LightspeedDash(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 060015c7 (1.10.1 RVA 0x77BFE0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_LightspeedDash(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}

		// 060015c8 (1.10.1 RVA 0x77C0A0); the character is teleported onto the surface tracker and moves along it at its current speed.
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			Character character = user.GetAs<Character>();
			character.SetTrackingType(CharacterTrackingType.SurfaceTracker);
			character.Tracker.OnTeleport(character.WorldPosition);
			character.Tracker.OnSurfaceChanged += OnSurfaceChanged;
			character.DetectTurnAroundFromVelocity();
			float speed = character.WorldVelocityMagnitude;
			character.SetWorldVelocity(character.Tracker.TrackerRotation * (Vector3.forward * speed));
			GetAbility(character).OnSurfaceChanged(character.Tracker.TrackerLocation.m_surface);
			character.Storage.SetValue(ActorFSMKeys.LightspeedDashInvulnerable, character.IsInvulnerable());
		}

		// 060015c9 (1.10.1 RVA 0x77C9D0); the target spline is cleared when the look ahead leaves the tracker or the
		// character becomes invulnerable while the dash had not been (inverted check preserved from the native body).
		protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			base.DoUpdate(user, updateContext);
			Character character = user.GetAs<Character>();
			CharacterAbility_LightspeedDash ability = GetAbility(character);
			character.Tracker.UpdateOnTrackerSpline(updateContext.DeltaTime, false);
			float forwardSpeed = character.WorldVelocityMagnitude;
			Vector3 forwardDirection = character.Tracker.TrackerRotation * Vector3.forward;
			ability.UpdateForwardSpeed(ref forwardSpeed, ref forwardDirection, updateContext.DeltaTime, 1f, 1f, null, !character.GetModifierValue<bool>((int)GameplayModifierType.AutoRail));
			character.SetWorldVelocity(forwardDirection * forwardSpeed);
			OverrideProxy(character);
			bool dashInvulnerable = character.Storage.GetValue(ActorFSMKeys.LightspeedDashInvulnerable, false, true);
			bool invulnerable = character.IsInvulnerable();
			if (dashInvulnerable)
			{
				character.Storage.SetValue(ActorFSMKeys.LightspeedDashInvulnerable, invulnerable);
			}
			if (!character.Tracker.LookAheadIsOnTracker() || (!dashInvulnerable && invulnerable))
			{
				ClearTargetSpline(character.Storage);
			}
		}

		// 060015ca (1.10.1 RVA 0x77D780)
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnLeave(user, action);
			Character character = user.GetAs<Character>();
			GetAbility(character).OnSurfaceChanged(null);
			character.Tracker.OnSurfaceChanged -= OnSurfaceChanged;
			character.DetectTracking();
			ResetProxy(character);
			ClearTargetSpline(character.Storage);
			character.Storage.RemoveValue<bool>(ActorFSMKeys.LightspeedDashInvulnerable);
		}

		// 060015cb (1.10.1 RVA 0x77C960)
		private void OnSurfaceChanged(Character character, SurfaceLocation fromLocation, SurfaceLocation toLocation)
		{
			GetAbility(character).OnSurfaceChanged(toLocation.m_surface);
		}

		// 060015cc (1.10.1 RVA 0x77D680)
		private void ClearTargetSpline(IGraphStorage storage)
		{
			storage.RemoveValue<ISpline>(ActorFSMKeys.LightspeedDashTarget);
		}

		// 060015cd (1.10.1 RVA 0x77D550); the visual proxy is tipped a quarter turn about the character's right axis.
		private void OverrideProxy(Character character)
		{
			character.VisualProxy.OverrideRotation(character.WorldRotation * Quaternion.AngleAxis(90f, Vector3.right));
		}

		// 060015ce (1.10.1 RVA 0x77D9E0)
		private void ResetProxy(Character character)
		{
			character.VisualProxy.StopOverridingRotation();
		}
	}
}
