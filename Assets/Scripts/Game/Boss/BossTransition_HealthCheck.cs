using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000245; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("CheckHealthIs")]
	[GraphNodeMenuFormat("Boss/{0}")]
	public class BossTransition_HealthCheck : FSMTransition
	{
		// Original nested 02000246.
		[Serializable]
		[FiniteStateMachineJsonCtorArgs]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		protected class HealthJSONCtorArgs
		{
			public int Value;

			public FSMTransitionCheckInt.Comparison Evaluation;
			// 06000c7b (1.10.1 RVA 0x710320): implicit constructor.
		}

		protected readonly int m_evalutionValue;

		protected readonly FSMTransitionCheckInt.Comparison m_evalutionMethod;

		// 06000c77 (1.10.1 RVA 0x710170)
		protected BossTransition_HealthCheck(FiniteStateMachine fsm, FSMIdentifier transitionId, HealthJSONCtorArgs ctorArgs) : base(fsm, transitionId)
		{
			m_evalutionValue = ctorArgs.Value;
			m_evalutionMethod = ctorArgs.Evaluation;
		}

		// 06000c78 (1.10.1 RVA 0x7101A0)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new BossTransition_HealthCheck(fsm, transitionId, JsonUtility.FromJson<HealthJSONCtorArgs>(jsonCtorArgs));
		}

		// 06000c79 (1.10.1 RVA 0x710240): compares the boss's current health with the configured value; an undefined
		// comparison never fires.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			Boss boss = user.GetAs<Boss>();
			switch (m_evalutionMethod)
			{
				case FSMTransitionCheckInt.Comparison.GreaterThan:
					return boss.CurrentHealth > m_evalutionValue;
				case FSMTransitionCheckInt.Comparison.GreaterThanEqual:
					return boss.CurrentHealth >= m_evalutionValue;
				case FSMTransitionCheckInt.Comparison.LessThan:
					return boss.CurrentHealth < m_evalutionValue;
				case FSMTransitionCheckInt.Comparison.LessThanEqual:
					return boss.CurrentHealth <= m_evalutionValue;
				case FSMTransitionCheckInt.Comparison.Equal:
					return boss.CurrentHealth == m_evalutionValue;
				default:
					return false;
			}
		}

		// 06000c7a (1.10.1 RVA 0x710310)
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnLeave(user, action);
		}
	}
}
