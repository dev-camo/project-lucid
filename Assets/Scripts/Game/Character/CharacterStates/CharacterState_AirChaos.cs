using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000352; bodies translated from the 1.10.1 x86-64 binary (shared generic code).
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class CharacterState_AirChaos<AbilityType, AbilityDefType> : CharacterState_AirAbility
		where AbilityType : CharacterAbility_AirChaos<AbilityDefType>
		where AbilityDefType : CharacterAbilityDefinition_AirChaos
	{
		// 06001518 (1.10.1 RVA 0xE6B2E0)
		protected CharacterState_AirChaos(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 06001519 (1.10.1 RVA 0xE6B2F0). Camera heading override is disabled while the
		// ability has no heading override of its own.
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			Character character = user.GetAs<Character>();
			if (TryGetAbility(character, out AbilityType ability))
			{
				ability.DoOnActivation();
				character.Storage.SetValue(ActorFSMKeys.StaminaUIDisabled, true);
				character.Storage.SetValue(ActorFSMKeys.CameraHeadingOverrideDisabled, ability.CameraHeadingOverride == null);
			}
		}

		// 0600151a (1.10.1 RVA 0xE6B530). While the time scale delay is running (or the
		// ability is missing) the air update is skipped and velocity is zeroed.
		protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			Character character = user.GetAs<Character>();
			if (TryGetAbility(character, out AbilityType ability) && ability.UpdateState(updateContext.DeltaTime))
			{
				base.DoUpdate(user, updateContext);
			}
			else
			{
				character.SetWorldVelocity(Vector3.zero);
			}
		}

		// 0600151b (1.10.1 RVA 0xE6B620)
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnLeave(user, action);
			Character character = user.GetAs<Character>();
			if (TryGetAbility(character, out AbilityType ability))
			{
				ability.DoOnDeactivation();
				character.Storage.RemoveValue<bool>(ActorFSMKeys.StaminaUIDisabled);
				character.Storage.RemoveValue<bool>(ActorFSMKeys.CameraHeadingOverrideDisabled);
			}
		}

		// 0600151c (1.10.1 RVA 0xE6B840)
		private bool TryGetAbility(Character character, out AbilityType ability)
		{
			ability = GetAbility(character) as AbilityType;
			return ability != null;
		}
	}
}
