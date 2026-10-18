using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020002a2; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class AnimatorRandomTrigger : StateMachineBehaviour
	{
		[SerializeField]
		private string m_triggerName;

		[SerializeField]
		private string m_randomIntName = "Idle_Index[Int]";

		[SerializeField]
		private int m_randomIntSetOnEnter;

		[SerializeField]
		private int m_numberOfStates;

		[SerializeField]
		private float m_minDelay = 1f;

		[SerializeField]
		private float m_maxDelay = 5f;

		[SerializeField]
		private TimeCategory m_timeCategory = TimeCategory.PlayerAnimation;

		private int m_randomIndexParamHash;

		private float m_nextTriggerTime = -1f;

		private int m_triggerHash;

		private readonly SystemRef<TimeManager> m_timeManagerRef = ProcessManager.GetSystemRef<TimeManager>(null, true);

		// 06000e81 (1.10.1 RVA 0x726D50): the random index parameter is written even
		// when its name is empty, through the hash from an earlier entry.
		public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
		{
			if (!string.IsNullOrEmpty(m_triggerName))
				m_triggerHash = Animator.StringToHash(m_triggerName);
			if (!string.IsNullOrEmpty(m_randomIntName))
				m_randomIndexParamHash = Animator.StringToHash(m_randomIntName);
			animator.SetInteger(m_randomIndexParamHash, m_randomIntSetOnEnter);
			if (m_minDelay > m_maxDelay)
				m_maxDelay = m_minDelay;
			m_nextTriggerTime = GetTotalTime() + Random.Range(m_minDelay, m_maxDelay);
		}

		// 06000e82 (1.10.1 RVA 0x726ED0): the random index is drawn from
		// 1..m_numberOfStates inclusive.
		public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
		{
			float totalTime = GetTotalTime();
			if (m_nextTriggerTime > 0f && totalTime >= m_nextTriggerTime)
			{
				if (!string.IsNullOrEmpty(m_randomIntName))
					animator.SetInteger(m_randomIndexParamHash, Random.Range(1, m_numberOfStates + 1));
				if (!string.IsNullOrEmpty(m_triggerName))
					animator.SetTrigger(m_triggerHash);
				m_nextTriggerTime = totalTime + Random.Range(m_minDelay, m_maxDelay);
			}
		}

		// 06000e83 (1.10.1 RVA 0x726E50): without a time manager the unscaled real
		// time is used.
		private float GetTotalTime()
		{
			if (m_timeManagerRef.IsNull())
				return Time.realtimeSinceStartup;
			return m_timeManagerRef.Get().GetTotalTime(m_timeCategory);
		}

		// 06000e84 (1.10.1 RVA 0x726FF0)
		public AnimatorRandomTrigger()
		{
		}
	}
}
