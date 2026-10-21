using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000379; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("HomingAttackGroundRolling")]
	[GraphNodeMenuFormat("Character/{0}")]
	public class CharacterState_HomingAttackGroundRolling : CharacterState_Ability<CharacterAbility_HomingAttackGroundRolling>
	{
		// 0600159e (1.10.1 RVA 0x779AC0)
		protected CharacterState_HomingAttackGroundRolling(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 0600159f (1.10.1 RVA 0x779B30)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_HomingAttackGroundRolling(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}

		// 060015a0 (1.10.1 RVA 0x779BF0)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			Character character = user.GetAs<Character>();
			CharacterAbility_HomingAttackGroundRolling ability = GetAbility(character);
			CharacterAbilityDefinition_HomingAttackGround definition = ability.Definition;
			character.BoostStamina.Activate(definition.AbilityType, definition.BoostDefinition, true);
			ability.SetActive(true);
		}

		// 060015a1 (1.10.1 RVA 0x779CE0); while a target is held the character steers towards it: straight at it when
		// it is above and inside the boost collider radius, along the ground when it is outside the radius, and otherwise
		// it keeps its heading.
		protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			base.DoUpdate(user, updateContext);
			Character character = user.GetAs<Character>();
			CharacterAbility_HomingAttackGroundRolling ability = GetAbility(character);
			character.ActiveTracking.OrientateToGround();
			float deltaTime = updateContext.DeltaTime;
			character.Collider.StickToCollider(deltaTime);
			if (!ability.UpdateTarget(deltaTime, out ActiveHomingTarget _, out float _, out Vector3 characterToTarget, out float _))
			{
				return;
			}
			Vector3 up = character.UpDirection;
			float heightAboveCharacter = Vector3.Dot(characterToTarget, up);
			Vector3 alongGround = characterToTarget - up * heightAboveCharacter;
			float speed = character.WorldVelocityMagnitude;
			Vector3 forwardDirection = character.LocalVelocity / speed;
			if (character.BoostStamina.ColliderRadiusSqr > alongGround.sqrMagnitude)
			{
				if (heightAboveCharacter > character.BoostStamina.ColliderRadius)
				{
					forwardDirection = characterToTarget.normalized;
				}
			}
			else
			{
				float turnAngle = Vector3.SignedAngle(character.ForwardDirection, alongGround, up);
				forwardDirection = Quaternion.AngleAxis(turnAngle, Vector3.up) * Vector3.forward;
			}
			ability.UpdateForwardSpeed(ref speed, ref forwardDirection, deltaTime);
			character.SetLocalVelocity(forwardDirection * speed);
		}

		// 060015a2 (1.10.1 RVA 0x77A090)
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnLeave(user, action);
			GetAbility(user.GetAs<Character>()).SetActive(false);
		}
	}
}
