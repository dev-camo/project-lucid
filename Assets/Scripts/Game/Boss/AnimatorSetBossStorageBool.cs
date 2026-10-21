using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020002a3; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class AnimatorSetBossStorageBool : StateMachineBehaviour
	{
		[SerializeField]
		private bool m_setOnEntry;

		[SerializeField]
		private string m_fsmVarOnEntry;

		[SerializeField]
		private bool m_entryValue;

		[SerializeField]
		private bool m_setOnExit;

		[SerializeField]
		private string m_fsmVarOnExit;

		[SerializeField]
		private bool m_exitValue;

		private bool m_varNamesCreated;

		private GraphStorageKey m_entryVarName;

		private GraphStorageKey m_exitVarName;

		private Boss m_boss;

		private SystemRef<BossManager> m_bossManagerRef;

		private BossManager m_bossManager;

		// 06000e85 (1.10.1 RVA 0x7270B0)
		private void RegisterBossManager(BossManager bossManager)
		{
			m_bossManager = bossManager;
		}

		// 06000e86 (1.10.1 RVA 0x7270D0): every entry re-resolves the manager and the boss that owns this animator. The
		// storage keys are built once, on the first entry, from whichever of the two variable names is non-empty.
		public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
		{
			m_bossManagerRef = ProcessManager.GetSystemRef<BossManager>(null, true);
			m_bossManagerRef.InvokeOnValid(RegisterBossManager);
			m_boss = m_bossManager.GetBossForAnimator(animator);
			if (!m_varNamesCreated)
			{
				if (!string.IsNullOrEmpty(m_fsmVarOnEntry))
				{
					m_entryVarName = new GraphStorageKey(m_fsmVarOnEntry, 0, 0);
				}
				if (!string.IsNullOrEmpty(m_fsmVarOnExit))
				{
					m_exitVarName = new GraphStorageKey(m_fsmVarOnExit, 0, 0);
				}
				m_varNamesCreated = true;
			}
			if (m_setOnEntry)
			{
				m_boss.Storage.SetValue(m_entryVarName, m_entryValue);
			}
		}

		// 06000e87 (1.10.1 RVA 0x727310): exit uses the boss found on the last entry.
		public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
		{
			if (m_setOnExit)
			{
				m_boss.Storage.SetValue(m_exitVarName, m_exitValue);
			}
		}

		// 06000e88 (1.10.1 RVA 0x727400)
		public AnimatorSetBossStorageBool()
		{
		}
	}
}
