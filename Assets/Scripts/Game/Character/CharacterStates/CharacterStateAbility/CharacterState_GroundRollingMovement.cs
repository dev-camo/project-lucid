using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200036c; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Character/{0}")]
	[GraphNodeDefaultName("GroundRollingMovement")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterState_GroundRollingMovement : CharacterState_Ground<CharacterAbilityDefinition_Rolling, CharacterAbility_Rolling>
	{
		// 06001576 (1.10.1 RVA 0x776790)
		protected CharacterState_GroundRollingMovement(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 06001577 (1.10.1 RVA 0x776800)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_GroundRollingMovement(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}

		// 06001578 (1.10.1 RVA 0x7768C0). Once the speed has dropped to the auto exit threshold (an unordered speed
		// does not count) the roll impulses end and rolling is no longer active.
		protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			base.DoUpdate(user, updateContext);
			Character character = user.GetAs<Character>();
			CharacterAbility_Rolling ability = GetAbility(character);
			float speed = character.WorldVelocityMagnitude;
			if (ability.Definition.AutoExitSpeedThreshold >= speed)
			{
				character.EndRollImpulses();
				character.Storage.RemoveValue<bool>(ActorFSMKeys.RollingActive);
			}
		}

		// 06001579 (1.10.1 RVA 0x776A80); above the ledge braking speed threshold ledge braking is ignored.
		protected override void UpdateLedgeBraking(Character character, LedgeBrakeParameters ledgeBrakeParameters)
		{
			CharacterAbility_Rolling ability = GetAbility(character);
			float speed = character.WorldVelocityMagnitude;
			if (speed >= ability.Definition.LedgeBrakingSpeedThreshold)
			{
				return;
			}
			base.UpdateLedgeBraking(character, ledgeBrakeParameters);
		}
	}
}
