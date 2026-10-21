using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200023d; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("BossTransition_ClearLineOfSight")]
	[GraphNodeMenuFormat("Boss/Fourmen/{0}")]
	public class BossTransition_ClearLineOfSight : FSMTransition
	{
		// Original nested 0200023e.
		[Serializable]
		[FiniteStateMachineJsonCtorArgs]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		protected class JSONCtorArgs
		{
			public float MaxDistanceToCheck;

			public float ConeOfViewAngle;
			// 06000c63 (1.10.1 RVA 0x70F410): implicit constructor.
		}

		// The original constructor never assigns this field; it stays zero.
		private readonly float m_preAttackWaitSeconds;

		private readonly float m_maxDistanceToCheck;

		private readonly float m_coneOfViewAngle;

		// 06000c60 (1.10.1 RVA 0x70F2C0)
		protected BossTransition_ClearLineOfSight(FiniteStateMachine fsm, FSMIdentifier transitionId, JSONCtorArgs ctorArgs) : base(fsm, transitionId)
		{
			m_maxDistanceToCheck = ctorArgs.MaxDistanceToCheck;
			m_coneOfViewAngle = ctorArgs.ConeOfViewAngle;
		}

		// 06000c61 (1.10.1 RVA 0x70F2F0)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new BossTransition_ClearLineOfSight(fsm, transitionId, JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs));
		}

		// 06000c62 (1.10.1 RVA 0x70F390): the base result is discarded; the whole cone must be clear.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			base.DoUpdate(user, context);
			return user.GetAs<Boss>().HasLineOfSightOnCharacter(m_maxDistanceToCheck, true, m_coneOfViewAngle);
		}
	}
}
