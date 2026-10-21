using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200024f; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("UsedClawCheck")]
	[GraphNodeMenuFormat("Boss/Crab/{0}")]
	public class BossTransition_UsedClawCheck : FSMTransition
	{
		// Original nested 02000250.
		[Serializable]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[FiniteStateMachineJsonCtorArgs]
		protected class ClawJSONCtorArgs
		{
			public BossAttackTypes Claw;

			public bool Deflated;
			// 06000c96 (1.10.1 RVA 0x711380): implicit constructor.
		}

		private BossAttackTypes m_claw;

		private bool m_clawDeflated;

		// 06000c93 (1.10.1 RVA 0x7110B0)
		protected BossTransition_UsedClawCheck(FiniteStateMachine fsm, FSMIdentifier transitionId, ClawJSONCtorArgs ctorArgs) : base(fsm, transitionId)
		{
			m_claw = ctorArgs.Claw;
			m_clawDeflated = ctorArgs.Deflated;
		}

		// 06000c94 (1.10.1 RVA 0x7110E0)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new BossTransition_UsedClawCheck(fsm, transitionId, JsonUtility.FromJson<ClawJSONCtorArgs>(jsonCtorArgs));
		}

		// 06000c95 (1.10.1 RVA 0x711180): the stored attack must be the configured claw's, and that claw's vulnerable point
		// must be in the configured deflated state. The point is read before the attack types are compared, and any other
		// configured claw never fires.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			Boss boss = user.GetAs<Boss>();
			BossAttackTypes attackType = user.Storage.GetValue(BossFSMKeys.BossAttackType, BossAttackTypes.None, true);
			switch (m_claw)
			{
				case BossAttackTypes.BossCrab_RightClaw:
					bool rightClawDestroyed = boss.IsComponentDestroyed(ActorAttachPointType.Boss_Crab_RightClaw_VulnerablePoint);
					return m_claw == attackType && m_clawDeflated == rightClawDestroyed;
				case BossAttackTypes.BossCrab_LeftClaw:
					bool leftClawDestroyed = boss.IsComponentDestroyed(ActorAttachPointType.Boss_Crab_LeftClaw_VulnerablePoint);
					return m_claw == attackType && m_clawDeflated == leftClawDestroyed;
				default:
					return false;
			}
		}
	}
}
