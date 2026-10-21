using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000393; bodies translated from the 1.10.1 x86-64 binary. The character flies to the
	// transporter its proximity ability locked onto, then is placed on it with the speed it had before.
	[GraphNodeMenuFormat("Character/{0}")]
	[GraphNodeDefaultName("TransporterHomingProximity")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterState_TransporterHomingProximity : CharacterState_Rail<CharacterAbility_TransporterProximity>
	{
		// 060015ff (1.10.1 RVA 0x77FD50)
		protected CharacterState_TransporterHomingProximity(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 06001600 (1.10.1 RVA 0x77FDC0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_TransporterHomingProximity(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}

		// 06001601 (1.10.1 RVA 0x77FE80)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			Character character = user.GetAs<Character>();
			character.Storage.SetValue(ActorFSMKeys.TransporterActive, true);
			character.Storage.SetValue(ActorFSMKeys.RailTargetTime, 0f);
		}

		// 06001602 (1.10.1 RVA 0x780090); while the homing target is held the character is flown straight at it,
		// at the target speed plus the ability's additional speed over the time spent homing, and turned
		// towards the target rotation. Without an active homing target, or when the ability's definition is not a
		// transporter proximity definition (in practice a missing definition), the transporter is no longer active.
		protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			base.DoUpdate(user, updateContext);
			Character character = user.GetAs<Character>();
			CharacterAbility_TransporterProximity ability = GetAbility(character);
			CharacterAbilityDefinition definition = ability.CharacterAbilityDefinition;
			if (!character.HomingPool.IsTargetActive() || !(definition is CharacterAbilityDefinition_TransporterProximity))
			{
				character.Storage.SetValue(ActorFSMKeys.TransporterActive, false);
				return;
			}
			ActiveHomingTarget target = character.HomingPool.ActiveTarget;
			Vector3 toTarget = target.Object.WorldPosition - character.Bounds.center;
			Vector3 direction = toTarget / toTarget.magnitude;
			float homingTime = character.Storage.AdjustValue(ActorFSMKeys.RailTargetTime, updateContext.DeltaTime);
			float speed = target.Speed + ability.GetAdditionalSpeed(homingTime);
			character.SetWorldVelocity(direction * speed);
			if (target.RotationalSpeed != 0f)
			{
				character.SetWorldRotation(Quaternion.RotateTowards(character.WorldRotation, target.Object.WorldRotation, target.RotationalSpeed * updateContext.DeltaTime));
			}
		}

		// 06001603 (1.10.1 RVA 0x7804F0); leaving with the target still held drops the character on it with the
		// velocity it had when it locked on. The RailTargetTime key is removed as a bool, as the original does.
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnLeave(user, action);
			Character character = user.GetAs<Character>();
			character.Storage.RemoveValue<bool>(ActorFSMKeys.TransporterActive);
			character.Storage.RemoveValue<bool>(ActorFSMKeys.RailTargetTime);
			if (character.HomingPool.IsTargetActive())
			{
				ActiveHomingTarget target = character.HomingPool.ActiveTarget;
				character.SetWorldPosition(target.Object.WorldPosition);
				character.SetWorldVelocity(target.RestoreVelocity);
				character.Tracker.OnTeleport(character.WorldPosition);
			}
			character.HomingPool.ClearTarget(false);
		}
	}
}
