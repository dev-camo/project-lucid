using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000372; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("HalfPipeTransfer")]
	[GraphNodeMenuFormat("Character/{0}")]
	public class CharacterState_HalfPipeTransfer : CharacterState_HalfPipe<CharacterAbilityDefinition_HalfPipeTransfer, CharacterAbility_HalfPipeTransfer>
	{
		// 0600158d (1.10.1 RVA 0x778D20)
		protected CharacterState_HalfPipeTransfer(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 0600158e (1.10.1 RVA 0x778D90)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_HalfPipeTransfer(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}

		// 0600158f (1.10.1 RVA 0x778E50)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			Character character = user.GetAs<Character>();
			character.Storage.SetValue(ActorFSMKeys.HalfPipeTransferNormalStart, character.UpDirection);
			character.Storage.SetValue(ActorFSMKeys.HalfPipeTransferRotationTime, 0f);
		}

		// 06001590 (1.10.1 RVA 0x779070); while the transfer is active the character turns from the entry normal to the hit normal
		// over the ability's hit time. Nothing happens once the rotation time has reached the hit time.
		protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			base.DoUpdate(user, updateContext);
			Character character = user.GetAs<Character>();
			if (!character.Storage.GetValue(ActorFSMKeys.HalfPipeTransferActive, false, true))
			{
				return;
			}
			CharacterAbility_HalfPipeTransfer ability = GetAbility(character);
			float rotationTime = character.Storage.GetValue(ActorFSMKeys.HalfPipeTransferRotationTime, 0f, true);
			if (!(ability.HitTime > rotationTime))
			{
				return;
			}
			rotationTime = Mathf.Min(rotationTime + updateContext.DeltaTime, ability.HitTime);
			character.Storage.SetValue(ActorFSMKeys.HalfPipeTransferRotationTime, rotationTime);
			Vector3 normalStart = character.Storage.GetValue(ActorFSMKeys.HalfPipeTransferNormalStart, Vector3.zero, true);
			Vector3 hitNormal = ability.HitNormal;
			Vector3 velocity = character.WorldVelocity;
			Quaternion from = Quaternion.LookRotation(velocity, normalStart);
			Quaternion to = Quaternion.LookRotation(velocity, hitNormal);
			character.SetWorldRotation(Quaternion.Slerp(from, to, rotationTime / ability.HitTime));
		}

		// 06001591 (1.10.1 RVA 0x779540); the start normal is removed as a Quaternion although it was stored as a Vector3.
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnLeave(user, action);
			Character character = user.GetAs<Character>();
			character.Storage.RemoveValue<Quaternion>(ActorFSMKeys.HalfPipeTransferNormalStart);
			character.Storage.RemoveValue<float>(ActorFSMKeys.HalfPipeTransferRotationTime);
			character.Storage.SetValue(ActorFSMKeys.HalfPipeTransferActive, false);
		}
	}
}
