using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200029f; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class AnimatorEnteredAttackStateAbilitySetter : StateMachineBehaviour
	{
		[SerializeField]
		private bool m_setAttackOnEnter;

		[SerializeField]
		private AbilityDefinition m_attackAbilityDefinition;

		private Boss m_boss;

		private SystemRef<BossManager> m_bossManagerRef;

		private BossManager m_bossManager;

		// 06000e76 (1.10.1 RVA 0x726A00)
		private void RegisterBossManager(BossManager bossManager)
		{
			m_bossManager = bossManager;
		}

		// 06000e77 (1.10.1 RVA 0x726A20): every entry re-resolves the manager and the
		// boss that owns this animator, whether or not m_setAttackOnEnter is set.
		public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
		{
			m_bossManagerRef = ProcessManager.GetSystemRef<BossManager>(null, true);
			m_bossManagerRef.InvokeOnValid(RegisterBossManager);
			m_boss = m_bossManager.GetBossForAnimator(animator);
			if (m_setAttackOnEnter && m_boss.TryGetAbilityFromType(m_attackAbilityDefinition.AbilityType, out ActorAbility ability) &&
				ability is IOffensiveAbility offensiveAbility)
				offensiveAbility.SetAttackAnimStarted();
		}

		// 06000e78 (1.10.1 RVA 0x726BF0)
		public AnimatorEnteredAttackStateAbilitySetter()
		{
		}
	}
}
