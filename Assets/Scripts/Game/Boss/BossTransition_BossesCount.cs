using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000237; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Boss/Fourmen/{0}")]
	[GraphNodeDefaultName("NumberOfBosses")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class BossTransition_BossesCount : FSMTransition
	{
		// Original nested 02000238.
		[Serializable]
		[FiniteStateMachineJsonCtorArgs]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		protected class JSONCtorArgs
		{
			public int BossesForTransition;
			// 06000c55 (1.10.1 RVA 0x70E850): implicit constructor.
		}

		private readonly int m_bossCount;

		// 06000c52 (1.10.1 RVA 0x70E6C0)
		protected BossTransition_BossesCount(FiniteStateMachine fsm, FSMIdentifier transitionId, JSONCtorArgs ctorArgs) : base(fsm, transitionId)
		{
			m_bossCount = ctorArgs.BossesForTransition;
		}

		// 06000c53 (1.10.1 RVA 0x70E6F0)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new BossTransition_BossesCount(fsm, transitionId, JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs));
		}

		// 06000c54 (1.10.1 RVA 0x70E790): the base result is discarded; fires while exactly the configured number of bosses
		// is listed on the Fourmen manager's boss manager.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			base.DoUpdate(user, context);
			return ProcessManager.GetSystem<FourmenManager>().BossCount() == m_bossCount;
		}
	}
}
