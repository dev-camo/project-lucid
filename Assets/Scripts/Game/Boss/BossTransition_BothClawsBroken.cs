using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200023b; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("BothClawsBroken")]
	[GraphNodeMenuFormat("Boss/Crab/{0}")]
	public class BossTransition_BothClawsBroken : FSMTransition
	{
		// Original nested 0200023c.
		[Serializable]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[FiniteStateMachineJsonCtorArgs]
		protected class DelayTransitionJSONCtorArgs
		{
			public float DelayTransitionFiringTime;
			// 06000c5f (1.10.1 RVA 0x70F2B0): implicit constructor.
		}

		private float m_delayTransitionFiringTime;

		// 06000c5b (1.10.1 RVA 0x70EED0)
		protected BossTransition_BothClawsBroken(FiniteStateMachine fsm, FSMIdentifier transitionId, DelayTransitionJSONCtorArgs ctorArgs) : base(fsm, transitionId)
		{
			m_delayTransitionFiringTime = ctorArgs.DelayTransitionFiringTime;
		}

		// 06000c5c (1.10.1 RVA 0x70EF00)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new BossTransition_BothClawsBroken(fsm, transitionId, JsonUtility.FromJson<DelayTransitionJSONCtorArgs>(jsonCtorArgs));
		}

		// 06000c5d (1.10.1 RVA 0x70EFA0): both claws' vulnerable points are read before either is tested, then the phase wait
		// accumulates until it exceeds the configured delay.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			Boss boss = user.GetAs<Boss>();
			bool leftClawDestroyed = boss.IsComponentDestroyed(ActorAttachPointType.Boss_Crab_LeftClaw_VulnerablePoint);
			bool rightClawDestroyed = boss.IsComponentDestroyed(ActorAttachPointType.Boss_Crab_RightClaw_VulnerablePoint);
			return leftClawDestroyed && rightClawDestroyed && user.Storage.AdjustValue(BossFSMKeys.PhaseTransitionWait, context.DeltaTime, 0f) > m_delayTransitionFiringTime;
		}

		// 06000c5e (1.10.1 RVA 0x70F110)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			user.Storage.SetValue(BossFSMKeys.PhaseTransitionWait, 0f);
			base.DoOnEnter(user, action);
		}
	}
}
