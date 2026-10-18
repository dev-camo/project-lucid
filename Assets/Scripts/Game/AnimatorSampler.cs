using System.Diagnostics;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020006db; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class AnimatorSampler : MonoBehaviour
	{
		[SerializeField]
		private Animator m_animator;

		[Range(0f, 1f)]
		[SerializeField]
		private float m_sampleFrame;

		// 060024fb (1.10.1 RVA 0x59CB70): the pose is sampled once and the Animator
		// is then left disabled.
		private void OnEnable()
		{
			SampleAnimatorState();
			m_animator.enabled = false;
			m_animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
		}

		// 060024fc (1.10.1 RVA 0x59CC00): m_sampleFrame is a normalised time in the
		// current state of the base layer.
		public void SampleAnimatorState()
		{
			AnimatorStateInfo stateInfo = m_animator.GetCurrentAnimatorStateInfo(0);
			m_animator.Play(stateInfo.shortNameHash, 0, m_sampleFrame);
			m_animator.Update(0f);
		}

		// 060024fd (1.10.1 RVA 0x59CC70): the fallback after the child search is a
		// plain reference test (0x59cd0f), not Unity's null comparison.
		private void OnValidate()
		{
			if (m_animator == null)
			{
				m_animator = gameObject.GetComponentInChildren<Animator>();
				m_animator ??= gameObject.AddComponent<Animator>();
			}
			if (m_animator.gameObject.activeInHierarchy)
				SampleAnimatorState();
		}

		// 060024fe (1.10.1 RVA 0x59CDC0)
		[Conditional("UNITY_EDITOR")]
		private void OnRenderObject()
		{
			SampleAnimatorState();
		}

		// 060024ff (1.10.1 RVA 0x59CE30)
		public AnimatorSampler()
		{
		}
	}
}
