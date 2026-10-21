using System;
using System.Collections.Generic;
using System.Text;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000373; bodies translated from the 1.10.1 x86-64 binary (shared generic
	// bodies at 0xE73850..0xE73D00). Flies the character at its active homing target.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class CharacterState_HomingAttackAir<TAbility, TAbilityDef> : CharacterState_Ability<TAbility>
		where TAbility : CharacterAbility_HomingAttackAir<TAbilityDef>
		where TAbilityDef : CharacterAbilityDefinition_HomingAttackAir
	{
		// Original nested 02000374.
		[FiniteStateMachineJsonCtorArgs]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Serializable]
		protected class HomingAttackJSONCtorArgs : AbilityJSONCtorArgs
		{
			public List<ActorAbilityType> IgnoreForceTriggerAbilityTypes;

			// 06001597 (1.10.1 RVA 0x13C62C0, shared generic)
			public HomingAttackJSONCtorArgs()
			{
			}
		}

		private readonly List<ActorAbilityType> m_ignoreForceTriggerAbilityTypes;

		// 06001592 (1.10.1 RVA 0xE73850, shared generic)
		protected CharacterState_HomingAttackAir(FiniteStateMachine fsm, FSMIdentifier stateId, HomingAttackJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
			m_ignoreForceTriggerAbilityTypes = ctorArgs.IgnoreForceTriggerAbilityTypes;
		}

		// 06001593 (1.10.1 RVA 0xE738A0, shared generic); the ability may only force the trigger while none of the
		// ignored ability types is present on the character.
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			Character character = user.GetAs<Character>();
			TAbility ability = GetAbility(character);
			bool enableForceTrigger = true;
			foreach (ActorAbilityType abilityType in m_ignoreForceTriggerAbilityTypes)
			{
				if (character.HasAbilityType(abilityType))
				{
					enableForceTrigger = false;
					break;
				}
			}
			ability.SetActive(true, enableForceTrigger);
		}

		// 06001594 (1.10.1 RVA 0xE73A70, shared generic); moves towards the target at the homing speed, never overshooting
		// it within the frame, and faces the direction of travel.
		protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			base.DoUpdate(user, updateContext);
			Character character = user.GetAs<Character>();
			TAbility ability = GetAbility(character);
			float deltaTime = updateContext.DeltaTime;
			UpdateRemainTimer(character, deltaTime);
			if (!ability.UpdateTarget(deltaTime, out ActiveHomingTarget activeTarget, out float time, out Vector3 characterToTarget, out float characterToTargetDistanceSqr))
			{
				return;
			}
			float distance = Mathf.Sqrt(characterToTargetDistanceSqr);
			Vector3 direction = characterToTarget / distance;
			float speed = activeTarget.Speed + ability.GetAdditionalSpeed(time);
			float stepDistance = speed * deltaTime;
			if (stepDistance >= distance)
			{
				speed = distance / deltaTime;
			}
			character.SetWorldVelocity(direction * speed);
			character.SetWorldRotation(Quaternion.LookRotation(direction, character.WorldUp));
		}

		// 06001595 (1.10.1 RVA 0xE73C50, shared generic)
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnLeave(user, action);
			GetAbility(user.GetAs<Character>()).SetActive(false);
		}

		// 06001596 (1.10.1 RVA 0xE73D00, shared generic)
		public override void DebugInfo(Actor actor, StringBuilder stringInfoBuilder)
		{
			base.DebugInfo(actor, stringInfoBuilder);
			Character character = actor as Character;
			if (character == null)
			{
				return;
			}
			ActiveHomingTarget activeTarget = character.HomingPool.ActiveTarget;
			if (activeTarget == null || activeTarget.Object.Target == null)
			{
				stringInfoBuilder.AppendLine("ERROR: No targeted object found.");
				return;
			}
			float distance = (activeTarget.Object.WorldPosition - character.WorldPosition).magnitude;
			stringInfoBuilder.AppendLine("Target = " + activeTarget.Object.Target.name);
			stringInfoBuilder.Append(string.Format("Speed = {0:F2}, ", activeTarget.Speed));
			stringInfoBuilder.AppendLine(string.Format("Distance = {0:F2}", distance));
		}
	}
}
