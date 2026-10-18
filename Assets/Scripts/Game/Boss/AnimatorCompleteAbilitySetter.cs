using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200029e; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class AnimatorCompleteAbilitySetter : StateMachineBehaviour
	{
		[SerializeField]
		private bool m_triggerOnEntry;

		[SerializeField]
		private bool m_triggerOnExit;

		[SerializeField]
		private AbilityDefinition m_abilityDefinition;

		private Boss m_boss;

		private SystemRef<BossManager> m_bossManagerRef;

		private BossManager m_bossManager;

		// 06000e72 (1.10.1 RVA 0x726720)
		private void RegisterBossManager(BossManager bossManager)
		{
			m_bossManager = bossManager;
		}

		// 06000e73 (1.10.1 RVA 0x726740): every entry re-resolves the manager and the
		// boss that owns this animator, whether or not m_triggerOnEntry is set.
		public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
		{
			m_bossManagerRef = ProcessManager.GetSystemRef<BossManager>(null, true);
			m_bossManagerRef.InvokeOnValid(RegisterBossManager);
			m_boss = m_bossManager.GetBossForAnimator(animator);
			if (m_triggerOnEntry && m_boss.TryGetAbilityFromType(m_abilityDefinition.AbilityType, out ActorAbility ability) &&
				ability is IAbilityAnimatorTriggerable triggerable)
				triggerable.SetAnimationComplete();
		}

		// 06000e74 (1.10.1 RVA 0x726910): exit uses the boss found on the last entry.
		public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
		{
			if (m_triggerOnExit && m_boss.TryGetAbilityFromType(m_abilityDefinition.AbilityType, out ActorAbility ability) &&
				ability is IAbilityAnimatorTriggerable triggerable)
				triggerable.SetAnimationComplete();
		}

		// 06000e75 (1.10.1 RVA 0x7269F0)
		public AnimatorCompleteAbilitySetter()
		{
		}
	}
}
