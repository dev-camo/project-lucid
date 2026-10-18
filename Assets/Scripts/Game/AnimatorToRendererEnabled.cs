using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020000a5; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[RequireComponent(typeof(Animator))]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class AnimatorToRendererEnabled : MonoBehaviour
	{
		[SerializeField]
		private Animator m_animator;

		[SerializeField]
		private string m_animParameter;

		[Tooltip("Renderers will be enabled when Animator Parameter float is less than threshold, disabled if more than.")]
		[SerializeField]
		private Renderer[] m_onAnimThresholdNotMet;

		[Tooltip("Renderers will be enabled when Animator Parameter float is more than threshold, disabled if less than.")]
		[SerializeField]
		private Renderer[] m_onAnimThresholdMet;

		[SerializeField]
		private float m_threshold = 0.5f;

		private bool m_thresholdMet;

		private int m_animParameterHash;

		// 06000549 (1.10.1 RVA 0x52F940)
		private void OnEnable()
		{
			if (string.IsNullOrEmpty(m_animParameter))
				enabled = false;
		}

		// 0600054a (1.10.1 RVA 0x52F980): starts from the not-met state.
		private void Start()
		{
			m_animParameterHash = Animator.StringToHash(m_animParameter);
			SetRenderers(false);
		}

		// 0600054b (1.10.1 RVA 0x52FAA0): the threshold is met only strictly above it,
		// and renderers are touched only when that changes.
		private void LateUpdate()
		{
			bool thresholdMet = m_animator.GetFloat(m_animParameterHash) > m_threshold;
			if (thresholdMet == m_thresholdMet)
				return;
			m_thresholdMet = thresholdMet;
			SetRenderers(thresholdMet);
		}

		// 0600054c (1.10.1 RVA 0x52F9B0): only the met list skips missing renderers;
		// the not-met list is skipped only as a whole when it is null.
		private void SetRenderers(bool thresholdMet)
		{
			foreach (Renderer renderer in m_onAnimThresholdMet)
			{
				if (renderer != null)
					renderer.enabled = thresholdMet;
			}
			if (m_onAnimThresholdNotMet != null)
			{
				foreach (Renderer renderer in m_onAnimThresholdNotMet)
					renderer.enabled = !thresholdMet;
			}
		}

		// 0600054d (1.10.1 RVA 0x52FAF0)
		private void Reset()
		{
			m_animator = GetComponent<Animator>();
		}

		// 0600054e (1.10.1 RVA 0x52FB40)
		public AnimatorToRendererEnabled()
		{
		}
	}
}
