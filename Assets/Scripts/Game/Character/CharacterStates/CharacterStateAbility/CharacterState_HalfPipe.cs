using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200036f; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Character/{0}")]
	public abstract class CharacterState_HalfPipe<TAbilityDefType, TAbilityType> : CharacterState_Ability<TAbilityType>
		where TAbilityDefType : CharacterAbilityDefinition_HalfPipe
		where TAbilityType : CharacterAbility_HalfPipe<TAbilityDefType>
	{
		// 06001582 (1.10.1 RVA 0xE73190)
		protected CharacterState_HalfPipe(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 06001583 (1.10.1 RVA 0xE731C0)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			Character character = user.GetAs<Character>();
			character.Storage.SetValue(ActorFSMKeys.HalfPipeGravityOnEnter, character.GravityNormalised);
		}

		// 06001584 (1.10.1 RVA 0xE73340); while the gravity direction is unchanged and the ability is not in the air, the
		// air-motion gravity multiplier and velocity clamp apply. Otherwise the half pipe flags are cleared.
		protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			base.DoUpdate(user, updateContext);
			Character character = user.GetAs<Character>();
			TAbilityType ability = GetAbility(character);
			bool airActive = ability.AirActive;
			Vector3 gravityNormalised = character.GravityNormalised;
			Vector3 gravityOnEnter = character.Storage.GetValue(ActorFSMKeys.HalfPipeGravityOnEnter, Vector3.zero, true);
			if (!airActive && gravityNormalised == gravityOnEnter)
			{
				float gravityMultiplier = ability.GetGravityMultiplier(character.WorldVelocity, out Vector3 clampedVelocity);
				AdjustGravityMultiplier(character, gravityMultiplier);
				character.SetWorldVelocity(clampedVelocity);
			}
			else
			{
				character.Storage.SetValue(ActorFSMKeys.HalfPipeColliderActive, false);
				character.Storage.SetValue(ActorFSMKeys.HalfPipeTransferActive, false);
			}
		}

		// 06001585 (1.10.1 RVA 0xE73700)
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnLeave(user, action);
			user.GetAs<Character>().Storage.RemoveValue<Vector3>(ActorFSMKeys.HalfPipeGravityOnEnter);
		}
	}
}
