using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000369; bodies translated from the 1.10.1 x86-64 binary (shared generic bodies at
	// 0xE6F680..0xE6FA00). Entering activates the boost stamina, and a character slower than the minimum input speed
	// turns to the input direction first.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class CharacterState_GroundBoostBase<AbilityDefType, AbilityType> : CharacterState_Ground<AbilityDefType, AbilityType>
		where AbilityDefType : CharacterAbilityDefinition_MovementGroundBoostBase
		where AbilityType : CharacterAbility_MovementGroundBoostBase<AbilityDefType>
	{
		// 0600156d (1.10.1 RVA 0xE6F680, shared generic)
		protected CharacterState_GroundBoostBase(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 0600156e (1.10.1 RVA 0xE6F6C0, shared generic)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			Character character = user.GetAs<Character>();
			AbilityType ability = GetAbility(character);
			AbilityDefType definition = ability.Definition;
			character.Storage.RemoveValue<bool>(ActorFSMKeys.RollingActive);
			character.BoostStamina.Activate(definition.AbilityType, definition.BoostDefinition);
			if (character.WorldVelocityMagnitude > character.Constants.MovementInputSpeedMin)
			{
				return;
			}
			character.SetWorldRotation(character.GetIntendedTurnRotation());
		}

		// 0600156f (1.10.1 RVA 0xE6F8E0, shared generic). A boost that is still triggered when the state ends keeps
		// the boost suspended for the definition's suspend time.
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnLeave(user, action);
			Character character = user.GetAs<Character>();
			AbilityType ability = GetAbility(character);
			AbilityDefType definition = ability.Definition;
			if (ability.IsTriggered)
			{
				float suspendTime = definition.BoostDefinition.SuspendTime;
				if (suspendTime > 0f)
				{
					character.BoostStamina.SuspendTimer = suspendTime;
				}
			}
		}

		// 06001570 (1.10.1 RVA 0xE6F9F0, shared generic)
		protected override bool CanQueueActions()
		{
			return false;
		}
	}
}
