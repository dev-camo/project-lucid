using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 0200038f; bodies translated from the 1.10.1 x86-64 binary (shared generic bodies at
	// 0xE76220..0xE76440). Entering activates the boost stamina; queued actions are never taken over.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class CharacterState_RailMovementBoostBase<TAbility, TAbilityDef> : CharacterState_RailMovement<TAbility, TAbilityDef>
		where TAbility : CharacterAbility_MovementRailBoostBase<TAbilityDef>
		where TAbilityDef : CharacterAbilityDefinition_MovementRailBoostBase
	{
		// 060015f2 (1.10.1 RVA 0xE76220, shared generic)
		protected CharacterState_RailMovementBoostBase(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 060015f3 (1.10.1 RVA 0xE76260, shared generic)
		protected override bool CanQueueActions()
		{
			return false;
		}

		// 060015f4 (1.10.1 RVA 0xE76270, shared generic)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			Character character = user.GetAs<Character>();
			TAbility ability = GetAbility(character);
			TAbilityDef definition = ability.Definition;
			character.BoostStamina.Activate(definition.AbilityType, definition.BoostDefinition);
			ability.SetActive(true);
		}

		// 060015f5 (1.10.1 RVA 0xE76350, shared generic). A boost that is still triggered when the state ends keeps
		// the boost suspended for the definition's suspend time.
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnLeave(user, action);
			Character character = user.GetAs<Character>();
			TAbility ability = GetAbility(character);
			TAbilityDef definition = ability.Definition;
			if (ability.IsTriggered)
			{
				float suspendTime = definition.BoostDefinition.SuspendTime;
				if (suspendTime > 0f)
				{
					character.BoostStamina.SuspendTimer = suspendTime;
				}
			}
			ability.SetActive(false);
		}
	}
}
