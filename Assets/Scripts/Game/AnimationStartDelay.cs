using System.Collections;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020009e1; bodies translated from the 1.10.1 x86-64 binary.
	[RequireComponent(typeof(Animation))]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class AnimationStartDelay : MonoBehaviour
	{
		[SerializeField]
		private float m_delay;

		private Animation m_animation;

		// 060038d5 (1.10.1 RVA 0x6A6780)
		private void OnEnable()
		{
			m_animation = GetComponent<Animation>();
			if (m_delay > 0f)
				StartCoroutine(StartDelay());
			else
				m_animation.Play();
		}

		// 060038d6 (1.10.1 RVA 0x6A68C0): the original stops a freshly created
		// iterator, which never matches the running one.
		private void OnDisable()
		{
			StopCoroutine(StartDelay());
		}

		// 060038d7 (1.10.1 RVA 0x6A6850); <StartDelay>d__4 060038d9..060038de.
		private IEnumerator StartDelay()
		{
			yield return new WaitForSeconds(m_delay);
			m_animation.Play();
		}

		// 060038d8 (1.10.1 RVA 0x6A6950)
		public AnimationStartDelay()
		{
		}
	}
}
