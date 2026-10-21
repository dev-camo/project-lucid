using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200039d and its iterator <ControlLock>d__6 (0200039f); bodies translated from the
	// 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Character/Trigger/{0}")]
	[GraphNodeDefaultName("TriggerGamePlayControl")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterState_TriggerGamePlayControl : FSMState_Trigger
	{
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Serializable]
		protected class JSONCtorArgs : TriggerJSONCtorArgs
		{
			public float ControlLockTime;

			public TimeCategory TimeCategory = TimeCategory.PlayerPhysics;

			// 0600162a (1.10.1 RVA 0x7831E0)
			public JSONCtorArgs()
			{
			}
		}

		private readonly float m_controlLockTime;

		private readonly TimeCategory m_timeCategory;

		// 06001626 (1.10.1 RVA 0x782F90)
		protected CharacterState_TriggerGamePlayControl(FiniteStateMachine fsm, FSMIdentifier stateId, JSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
			m_controlLockTime = ctorArgs.ControlLockTime;
			m_timeCategory = ctorArgs.TimeCategory;
		}

		// 06001627 (1.10.1 RVA 0x782FD0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_TriggerGamePlayControl(fsm, transitionId, JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs));
		}

		// 06001628 (1.10.1 RVA 0x783080)
		protected override void InvokeTriggerStart(IGraphUser user, FSMStateChangeAction action)
		{
			CoroutineUtils.RunCoroutine(ControlLock(user.GetAs<Character>()));
		}

		// 06001629 (1.10.1 RVA 0x783140); the controls stay disabled for the lock time and are then released.
		private IEnumerator ControlLock(Character character)
		{
			StackableDataHandle handle = character.AddModifierOverride((int)GameplayModifierType.ControlsEnabled, false);
			yield return TimeScaledUtilities_SDT.WaitForFixedSeconds(m_controlLockTime, m_timeCategory);
			character.RemoveModifierOverrides(handle);
		}
	}
}
