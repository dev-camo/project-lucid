using System.Text;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000389; bodies translated from the 1.10.1 x86-64 binary (shared generic
	// bodies at 0xE753D0..0xE76170). Flies the character along to its active rail homing target.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[GraphNodeMenuFormat("Character/{0}")]
	public abstract class CharacterState_RailHoming<TAbility, TAbilityDef> : CharacterState_Rail<TAbility>
		where TAbility : CharacterAbility_TargetingHoming<TAbilityDef>
		where TAbilityDef : CharacterAbilityDefinition_Targeting
	{
		// 060015d6 (1.10.1 RVA 0xE753D0, shared generic)
		protected CharacterState_RailHoming(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 060015d7 (1.10.1 RVA 0xE75400, shared generic)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			Character character = user.GetAs<Character>();
			character.Storage.SetValue(ActorFSMKeys.RailTargetTime, 0f);
			character.Storage.SetValue(ActorFSMKeys.RailTargetCancelled, false);
			// The original evaluates the pool's active target test here and discards the result.
			character.HomingPool.IsTargetActive();
		}

		// 060015d8 (1.10.1 RVA 0xE75610, shared generic); without an active target the rail target is cancelled. The
		// target is also cancelled once the homing has timed out or the way to it is blocked. Otherwise the character
		// is flown at the target without overshooting it and turned towards the target's rotation.
		protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			base.DoUpdate(user, updateContext);
			Character character = user.GetAs<Character>();
			if (!character.HomingPool.IsTargetActive())
			{
				character.Storage.SetValue(ActorFSMKeys.RailTargetCancelled, true);
				return;
			}
			Vector3 center = character.Bounds.center;
			ActiveHomingTarget activeTarget = character.HomingPool.ActiveTarget;
			Vector3 targetPosition = activeTarget.Object.WorldPosition;
			float deltaTime = updateContext.DeltaTime;
			CharacterAbilityDefinition_RailsHoming definition = GetRailsHomingDefinition(character);
			float time = character.Storage.AdjustValue(ActorFSMKeys.RailTargetTime, deltaTime, 0f);
			// The native comparison is unordered-true, so a NaN time is still inside the timeout.
			bool timedOut = time >= definition.TimeoutSeconds;
			if (!timedOut)
			{
				Vector3 toTarget = targetPosition - center;
				float distance = toTarget.magnitude;
				Vector3 direction = toTarget / distance;
				if (!Physics.Raycast(center, direction, distance, character.CollisionMask, QueryTriggerInteraction.Ignore))
				{
					TAbility ability = GetAbility(character);
					float speed = ability.GetAdditionalSpeed(time) + activeTarget.Speed;
					float stepDistance = speed * deltaTime;
					if (stepDistance > distance)
					{
						speed = distance / deltaTime;
					}
					character.SetWorldVelocity(direction * speed);
					// A target without a rotational speed turns at up to 180 degrees per update.
					float rotationalSpeed = activeTarget.RotationalSpeed;
					float maxDegreesDelta = rotationalSpeed == 0f ? 180f : deltaTime * rotationalSpeed;
					character.SetWorldRotation(Quaternion.RotateTowards(character.WorldRotation, activeTarget.Object.WorldRotation, maxDegreesDelta));
					return;
				}
			}
			character.Storage.SetValue(ActorFSMKeys.RailTargetCancelled, true);
			character.HomingPool.ClearTarget();
		}

		// 060015d9 (1.10.1 RVA 0xE75C60, shared generic); an active target at the end of the state puts the character at
		// the target with its stored velocity.
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnLeave(user, action);
			Character character = user.GetAs<Character>();
			character.Storage.RemoveValue<bool>(ActorFSMKeys.RailTargetCancelled);
			if (character.HomingPool.IsTargetActive())
			{
				ActiveHomingTarget activeTarget = character.HomingPool.ActiveTarget;
				character.SetWorldPosition(activeTarget.Object.WorldPosition);
				character.SetWorldVelocity(activeTarget.RestoreVelocity);
				character.Tracker.OnTeleport(character.WorldPosition);
			}
			character.HomingPool.ClearTarget();
		}

		// 060015da (1.10.1 RVA 0xE75E30, shared generic)
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
			stringInfoBuilder.AppendLine("Target = " + activeTarget.Object.Target.name);
			stringInfoBuilder.AppendLine(string.Format("Speed = {0:F2}", activeTarget.Speed));
			float distance = (activeTarget.Object.WorldPosition - character.WorldPosition).magnitude;
			stringInfoBuilder.AppendLine(string.Format("Distance = {0:F2}", distance));
			stringInfoBuilder.AppendLine(string.Format("RotationalSpeed = {0:F2}", activeTarget.RotationalSpeed));
		}

		// 060015db (1.10.1 RVA 0xE76170, shared generic)
		private CharacterAbilityDefinition_RailsHoming GetRailsHomingDefinition(Character character)
		{
			return GetAbility(character).DefinitionBase as CharacterAbilityDefinition_RailsHoming;
		}
	}
}
