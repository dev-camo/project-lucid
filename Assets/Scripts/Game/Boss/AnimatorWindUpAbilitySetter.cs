using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020002a7; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class AnimatorWindUpAbilitySetter : StateMachineBehaviour
	{
		[SerializeField]
		private bool m_windUpOnEnter;

		[SerializeField]
		private bool m_woundUpOnExit;

		[SerializeField]
		private AbilityDefinition m_attackAbilityDefinition;

		private Boss m_boss;

		private SystemRef<BossManager> m_bossManagerRef;

		private BossManager m_bossManager;

		// 06000e95 (1.10.1 RVA 0x727A20)
		private void RegisterBossManager(BossManager bossManager)
		{
			m_bossManager = bossManager;
		}

		// 06000e96 (1.10.1 RVA 0x727A40): every entry re-resolves the manager and the
		// boss that owns this animator, whether or not m_windUpOnEnter is set.
		public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
		{
			m_bossManagerRef = ProcessManager.GetSystemRef<BossManager>(null, true);
			m_bossManagerRef.InvokeOnValid(RegisterBossManager);
			m_boss = m_bossManager.GetBossForAnimator(animator);
			if (m_windUpOnEnter && m_boss.TryGetAbilityFromType(m_attackAbilityDefinition.AbilityType, out ActorAbility ability) &&
				ability is IOffensiveAbility offensiveAbility)
				offensiveAbility.SetAttackWindingUp();
		}

		// 06000e97 (1.10.1 RVA 0x727C10): exit uses the boss found on the last entry.
		public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
		{
			if (m_woundUpOnExit && m_boss.TryGetAbilityFromType(m_attackAbilityDefinition.AbilityType, out ActorAbility ability) &&
				ability is IOffensiveAbility offensiveAbility)
				offensiveAbility.SetAttackWoundUp();
		}

		// 06000e98 (1.10.1 RVA 0x727CF0)
		public AnimatorWindUpAbilitySetter()
		{
		}
	}
}
