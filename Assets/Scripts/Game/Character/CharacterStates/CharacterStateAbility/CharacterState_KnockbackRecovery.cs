using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000383; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeDefaultName("KnockbackRecovery")]
	[GraphNodeMenuFormat("Character/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterState_KnockbackRecovery : CharacterState_Ability<CharacterAbility_Knockback>
	{
		// 060015bd (1.10.1 RVA 0x77B1F0)
		protected CharacterState_KnockbackRecovery(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 060015be (1.10.1 RVA 0x77B260)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_KnockbackRecovery(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}

		// 060015bf (1.10.1 RVA 0x77B320)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			Character character = user.GetAs<Character>();
			GetAbility(character).EnterRecovery();
		}

		// 060015c0 (1.10.1 RVA 0x77B3C0); a backward local velocity decays towards zero and is never pushed past it.
		protected override void DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			base.DoUpdate(user, context);
			Character character = user.GetAs<Character>();
			CharacterAbility_Knockback ability = GetAbility(character);
			Vector3 localVelocity = character.LocalVelocity;
			if (localVelocity.z >= 0f)
			{
				return;
			}
			localVelocity.z = Mathf.Min(0f, localVelocity.z + context.DeltaTime * ability.Definition.RecoveryDeceleration);
			character.SetLocalVelocity(localVelocity);
		}
	}
}
