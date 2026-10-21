using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200024a; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Boss/Fourmen/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[GraphNodeDefaultName("ActiveBossTimeout")]
	public class BossTransition_IsActiveBossTimeout : FSMTransition
	{
		// Original nested 0200024b.
		[Serializable]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[FiniteStateMachineJsonCtorArgs]
		protected class JSONCtorArgs
		{
			public float Timeout;
			// 06000c89 (1.10.1 RVA 0x710C50): implicit constructor.
		}

		private readonly float m_timeout;

		// 06000c86 (1.10.1 RVA 0x710A60)
		protected BossTransition_IsActiveBossTimeout(FiniteStateMachine fsm, FSMIdentifier transitionId, JSONCtorArgs ctorArgs) : base(fsm, transitionId)
		{
			m_timeout = ctorArgs.Timeout;
		}

		// 06000c87 (1.10.1 RVA 0x710A90)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new BossTransition_IsActiveBossTimeout(fsm, transitionId, JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs));
		}

		// 06000c88 (1.10.1 RVA 0x710B30): the base result is discarded; the active boss timer accumulates until it exceeds
		// the timeout.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			base.DoUpdate(user, context);
			return user.Storage.AdjustValue(BossFSMKeys.ActiveBossTimer, context.DeltaTime, 0f) > m_timeout;
		}
	}
}
