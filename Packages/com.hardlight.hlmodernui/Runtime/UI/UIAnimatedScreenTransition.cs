using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace Hardlight
{
	// Original HLModernUI.Runtime 0200000b; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class UIAnimatedScreenTransition : MonoBehaviour, IUIScreenTransition
	{
		[SerializeField]
		private Animation m_animation;

		[SerializeField]
		private AnimationClip m_transitionInClip;

		[SerializeField]
		private float m_transitionInSpeed = 1f;

		[SerializeField]
		private AnimationClip m_transitionOutClip;

		[SerializeField]
		private float m_transitionOutSpeed = 1f;

		[SerializeField]
		private UnityEvent m_onStartEvent;

		[SerializeField]
		private UnityEvent m_onMidpointReachedEvent;

		private Action m_midpointCallback;

		private Action m_completionCallback;

		private bool m_hasReachedMidpoint;

		// 06000016 (1.10.1 RVA 0x1A3A8F0)
		public bool HasReachedMidpoint => m_hasReachedMidpoint;

		// 06000017 (1.10.1 RVA 0x1A3A900): the clip plays before the callbacks are stored, and
		// m_hasReachedMidpoint is never cleared.
		public void StartTransition(Action onReachMidpointCallback, Action onCompletionCallback)
		{
			PlayAnimation(m_transitionInClip, m_transitionInSpeed);
			m_midpointCallback = onReachMidpointCallback;
			m_completionCallback = onCompletionCallback;
			m_onStartEvent?.Invoke();
		}

		// 06000018 (1.10.1 RVA 0x1A3AA20); lambda <ContinueTransition>b__13_0 0600001f.
		public void ContinueTransition()
		{
			StartCoroutine(this.WaitForUI(() => PlayAnimation(m_transitionOutClip, m_transitionOutSpeed)));
		}

		// 06000019 (1.10.1 RVA 0x1A3AAA0)
		public void Action_OnMidpointReached()
		{
			m_hasReachedMidpoint = true;
			m_midpointCallback?.Invoke();
			m_onMidpointReachedEvent?.Invoke();
		}

		// 0600001a (1.10.1 RVA 0x1A3AAE0)
		public void Action_OnCompletion()
		{
			CompleteTransition();
		}

		// 0600001b (1.10.1 RVA 0x1A3AB00)
		private void CompleteTransition()
		{
			m_completionCallback?.Invoke();
		}

		// 0600001c (1.10.1 RVA 0x1A3A9C0)
		private void PlayAnimation(AnimationClip clip, float playbackSpeed)
		{
			m_animation.clip = clip;
			m_animation[clip.name].speed = playbackSpeed;
			m_animation.Play();
		}

		// 0600001d (1.10.1 RVA 0x1A3AB20)
		private void Reset()
		{
			if (m_animation == null)
				m_animation = GetComponent<Animation>();
		}

		// 0600001e (1.10.1 RVA 0x1A3ABB0)
		public UIAnimatedScreenTransition()
		{
		}
	}
}
