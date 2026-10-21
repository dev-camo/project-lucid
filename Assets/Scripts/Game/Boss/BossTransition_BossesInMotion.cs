using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000239; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Boss/Fourmen/{0}")]
	[GraphNodeDefaultName("BossTransition_BossesInMotion")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class BossTransition_BossesInMotion : FSMTransition
	{
		// Original nested 0200023a.
		[Serializable]
		[FiniteStateMachineJsonCtorArgs]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		protected class JSONCtorArgs
		{
			public bool NoneInMotionCausesTransition;

			public float AdditionalPauseSeconds;
			// 06000c5a (1.10.1 RVA 0x70EEC0): implicit constructor.
		}

		private readonly bool m_noneInMotionCausesTransition;

		private readonly float m_additionalPauseSeconds;

		// 06000c56 (1.10.1 RVA 0x70E860)
		protected BossTransition_BossesInMotion(FiniteStateMachine fsm, FSMIdentifier transitionId, JSONCtorArgs ctorArgs) : base(fsm, transitionId)
		{
			m_noneInMotionCausesTransition = ctorArgs.NoneInMotionCausesTransition;
			m_additionalPauseSeconds = ctorArgs.AdditionalPauseSeconds;
		}

		// 06000c57 (1.10.1 RVA 0x70E890)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new BossTransition_BossesInMotion(fsm, transitionId, JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs));
		}

		// 06000c58 (1.10.1 RVA 0x70E940): the pause timer restarts at zero.
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			user.Storage.SetValue(BossFSMKeys.AllBossesMotionTimer, 0f);
		}

		// 06000c59 (1.10.1 RVA 0x70EAD0): the base result is discarded. While the motion state (some boss in motion, or
		// none when configured so) holds, the pause timer accumulates; once it is no longer below the additional pause the
		// timer is removed and the transition fires. When the state does not hold the timer is held at zero.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			base.DoUpdate(user, context);
			bool anyInMotion = ProcessManager.GetSystem<FourmenManager>().BossesInMotion.Count > 0;
			if (anyInMotion != m_noneInMotionCausesTransition)
			{
				// The pause is compared as `pause > timer` (ucomiss, ja), so a NaN timer is not below it and fires.
				if (m_additionalPauseSeconds > user.Storage.AdjustValue(BossFSMKeys.AllBossesMotionTimer, context.DeltaTime, 0f))
				{
					return false;
				}
				user.Storage.RemoveValue<float>(BossFSMKeys.AllBossesMotionTimer);
				return true;
			}
			user.Storage.SetValue(BossFSMKeys.AllBossesMotionTimer, 0f);
			return false;
		}
	}
}
