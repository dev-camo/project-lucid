using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200036e; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("GroundSpinDashCharge")]
	[GraphNodeMenuFormat("Character/{0}")]
	public class CharacterState_GroundSpinDashCharge : CharacterState_Ground<CharacterAbilityDefinition_MovementGroundSpinDashCharge, CharacterAbility_MovementGroundSpinDashCharge>
	{
		// 0600157c (1.10.1 RVA 0x776C70)
		protected CharacterState_GroundSpinDashCharge(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 0600157d (1.10.1 RVA 0x776CE0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_GroundSpinDashCharge(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}

		// 0600157e (1.10.1 RVA 0x776DA0)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			Character character = user.GetAs<Character>();
			GetAbility(character).OnCharge();
			character.VisualProxy.ToggleSmoothing(false);
		}

		// 0600157f (1.10.1 RVA 0x776E50). The rotation at the start of the update is slerped toward the intended turn
		// rotation by the angle and speed curves scaled by the delta time (without input it is unchanged), and its
		// forward direction orients the character on the plane of the collision contact normal.
		protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			Character character = user.GetAs<Character>();
			CharacterAbility_MovementGroundSpinDashCharge ability = GetAbility(character);
			CharacterAbilityDefinition_MovementGroundSpinDashCharge definition = ability.Definition;
			Quaternion rotation = character.WorldRotation;
			OrientateToGround(character);
			base.DoUpdate(user, updateContext);
			if (character.TryGetIntendedTurnRotation(out float intendedTurnAngle, out Quaternion intendedTurnRotation, false))
			{
				float turnRate = definition.RotationLerpByAngleMultiplier.Evaluate(Mathf.Abs(intendedTurnAngle)) * definition.RotationLerpByVelocityMultiplier.Evaluate(character.WorldVelocityMagnitude) * updateContext.DeltaTime;
				rotation = Quaternion.Slerp(rotation, intendedTurnRotation, turnRate);
			}
			Vector3 lookDirection = rotation * Vector3.forward;
			character.OrientateToPlaneWithLookDirection(character.Collider.GetCollisionContactNormal(), lookDirection);
			SetLaunchAnimation(character.Animator, ability.GetLaunchSpeed(character.SpinDashRoll.ChargeTime));
		}

		// 06001580 (1.10.1 RVA 0x7770B0). The proxy rotation the charge steered is handed back to the character.
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnLeave(user, action);
			Character character = user.GetAs<Character>();
			CharacterAbility_MovementGroundSpinDashCharge ability = GetAbility(character);
			character.SetWorldRotation(character.VisualProxy.CachedTransform.rotation);
			character.VisualProxy.StopOverridingRotation();
			character.VisualProxy.ToggleSmoothing(true);
			ability.OnLaunch();
			SetLaunchAnimation(character.Animator, 0f);
		}

		// 06001581 (1.10.1 RVA 0x777090)
		private void SetLaunchAnimation(ActorAnimator animator, float launchPower)
		{
			animator.TrySetFromAnimationType(m_stateAnimationDefinition, ActorAnimationType.DashLaunchPower, launchPower);
		}
	}
}
