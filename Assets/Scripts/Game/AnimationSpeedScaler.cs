using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200009c; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[RequireComponent(typeof(Animation))]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class AnimationSpeedScaler : MonoBehaviour
	{
		[SerializeField]
		private Animation m_animation;

		[SerializeField]
		private float m_minSpeed;

		[SerializeField]
		private float m_maxSpeed;

		// 0600052a (1.10.1 RVA 0x52EE70)
		private void Reset()
		{
			m_animation = GetComponent<Animation>();
		}

		// 0600052b (1.10.1 RVA 0x52EEC0)
		public void SetAnimationEnabled(bool animationEnabled)
		{
			m_animation.enabled = animationEnabled;
		}

		// 0600052c (1.10.1 RVA 0x52EED0): a zero speed rewinds to and holds the first
		// frame; any other value (NaN included) plays at the interpolated speed.
		public void SetAnimationSpeed(float normalisedSpeed)
		{
			float speed = Mathf.Clamp01(normalisedSpeed);
			if (speed == 0f)
			{
				if (m_animation.enabled)
				{
					m_animation.Rewind();
					m_animation.Sample();
					m_animation.enabled = false;
				}
				return;
			}
			if (!m_animation.enabled)
				m_animation.enabled = true;
			m_animation[m_animation.clip.name].normalizedSpeed = Mathf.Lerp(m_minSpeed, m_maxSpeed, speed);
		}

		// 0600052d (1.10.1 RVA 0x52EFC0)
		public AnimationSpeedScaler()
		{
		}
	}
}
