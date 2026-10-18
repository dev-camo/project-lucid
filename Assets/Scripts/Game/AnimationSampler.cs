using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020006da; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class AnimationSampler : MonoBehaviour
	{
		[SerializeField]
		private Animation m_animation;

		[Range(0f, 1f)]
		[SerializeField]
		private float m_sampleFrame;

		// 060024f7 (1.10.1 RVA 0x59C9A0): the pose is sampled once and the
		// Animation is then left disabled.
		private void OnEnable()
		{
			SampleAnimation();
			m_animation.enabled = false;
			m_animation.cullingType = AnimationCullingType.AlwaysAnimate;
		}

		// 060024f8 (1.10.1 RVA 0x59C9D0): m_sampleFrame is a fraction of the clip length.
		public void SampleAnimation()
		{
			if (m_animation.clip == null)
				return;
			float time = m_animation.clip.length * m_sampleFrame;
			m_animation.clip.SampleAnimation(gameObject, time);
		}

		// 060024f9 (1.10.1 RVA 0x59CA90)
		private void OnValidate()
		{
			if (m_animation == null && !gameObject.TryGetComponent(out m_animation))
				m_animation = gameObject.AddComponent<Animation>();
			SampleAnimation();
		}

		// 060024fa (1.10.1 RVA 0x59CB60)
		public AnimationSampler()
		{
		}
	}
}
