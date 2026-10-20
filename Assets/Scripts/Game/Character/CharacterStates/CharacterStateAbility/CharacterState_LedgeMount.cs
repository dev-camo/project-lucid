using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000384; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("LedgeMount")]
	[GraphNodeMenuFormat("Character/{0}")]
	public class CharacterState_LedgeMount : CharacterState_Ability<CharacterAbility_LedgeMount>
	{
		// 060015c1 (1.10.1 RVA 0x77B4D0)
		protected CharacterState_LedgeMount(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 060015c2 (1.10.1 RVA 0x77B540)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_LedgeMount(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}

		// 060015c3 (1.10.1 RVA 0x77B600); the character is placed on the ledge surface, looking against its up direction.
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			Character character = user.GetAs<Character>();
			CharacterAbility_LedgeMount ability = GetAbility(character);
			character.OrientateToPlaneWithLookDirection(ability.LedgeSurfaceNormal, -character.UpDirection);
			character.SetWorldPosition(ability.LedgeMountPoint);
			character.Storage.SetValue(ActorFSMKeys.LedgeMountTimeElapsed, 0f);
			character.Storage.SetValue(ActorFSMKeys.LedgeMountFinished, false);
		}

		// 060015c4 (1.10.1 RVA 0x77B890); the character is held still until the pause time has elapsed.
		protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			base.DoUpdate(user, updateContext);
			Character character = user.GetAs<Character>();
			CharacterAbility_LedgeMount ability = GetAbility(character);
			character.SetWorldVelocity(Vector3.zero);
			float elapsed = character.Storage.GetValue(ActorFSMKeys.LedgeMountTimeElapsed, 0f, true) + updateContext.DeltaTime;
			character.Storage.SetValue(ActorFSMKeys.LedgeMountTimeElapsed, elapsed);
			if (elapsed >= ability.Definition.PauseSeconds)
			{
				character.Storage.SetValue(ActorFSMKeys.LedgeMountFinished, true);
			}
		}

		// 060015c5 (1.10.1 RVA 0x77BC20); the mount velocity is applied in local space (upward on Y, forward on Z).
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnLeave(user, action);
			Character character = user.GetAs<Character>();
			CharacterAbilityDefinition_LedgeMount definition = GetAbility(character).Definition;
			character.SetLocalVelocity(new Vector3(0f, definition.MountVelocityUpward, definition.MountVelocityForward));
			character.Collider.GroundHitLastFrame.Invalidate();
			user.Storage.RemoveValue<float>(ActorFSMKeys.LedgeMountTimeElapsed);
			user.Storage.RemoveValue<bool>(ActorFSMKeys.LedgeMountFinished);
		}
	}
}
