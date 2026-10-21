using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000235; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[GraphNodeDefaultName("BodyBroken")]
	[GraphNodeMenuFormat("Boss/Crab/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class BossTransition_BodyBroken : FSMTransition
	{
		// Original nested 02000236.
		[Serializable]
		[FiniteStateMachineJsonCtorArgs]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		protected class DelayTransitionJSONCtorArgs
		{
			public float DelayTransitionFiringTime;
			// 06000c51 (1.10.1 RVA 0x70E6B0): implicit constructor.
		}

		private float m_delayTransitionFiringTime;

		// 06000c4c (1.10.1 RVA 0x70E1C0)
		protected BossTransition_BodyBroken(FiniteStateMachine fsm, FSMIdentifier transitionId, DelayTransitionJSONCtorArgs ctorArgs) : base(fsm, transitionId)
		{
			m_delayTransitionFiringTime = ctorArgs.DelayTransitionFiringTime;
		}

		// 06000c4d (1.10.1 RVA 0x70E1F0)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new BossTransition_BodyBroken(fsm, transitionId, JsonUtility.FromJson<DelayTransitionJSONCtorArgs>(jsonCtorArgs));
		}

		// 06000c4e (1.10.1 RVA 0x70E290): once the body's vulnerable point is destroyed, the phase wait accumulates until it
		// exceeds the configured delay.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			if (!user.GetAs<Boss>().IsComponentDestroyed(ActorAttachPointType.Boss_Crab_Body_VulnerablePoint))
			{
				return false;
			}
			return user.Storage.AdjustValue(BossFSMKeys.PhaseTransitionWait, context.DeltaTime, 0f) > m_delayTransitionFiringTime;
		}

		// 06000c4f (1.10.1 RVA 0x70E3D0)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			user.Storage.SetValue(BossFSMKeys.PhaseTransitionWait, 0f);
			base.DoOnEnter(user, action);
		}

		// 06000c50 (1.10.1 RVA 0x70E570): clears the stored attack type, then chains to the base OnEnter hook (0x70E6A6 jumps
		// to FSMTransition.DoOnEnter, not DoOnLeave).
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			user.GetAs<Boss>().Storage.SetValue(BossFSMKeys.BossAttackType, BossAttackTypes.None);
			base.DoOnEnter(user, action);
		}
	}
}
