using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
	// Original Game.Runtime 0200009a; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class AnimationEventListener : MonoBehaviour
	{
		// Original Game.Runtime 0200009b.
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Serializable]
		public class AnimationEventAction
		{
			[SerializeField]
			private string m_eventString;

			[SerializeField]
			private UnityEvent m_action;

			// 06000527 (1.10.1 RVA 0x52EE40)
			public string EventString => m_eventString;

			// 06000528 (1.10.1 RVA 0x52EE50)
			public UnityEvent Action => m_action;

			// 06000529 (1.10.1 RVA 0x52EE60)
			public AnimationEventAction()
			{
			}
		}

		[SerializeField]
		private List<AnimationEventAction> m_animatorEventActions;

		private readonly Dictionary<string, UnityEvent> m_indexedActions = new Dictionary<string, UnityEvent>();

		// 06000522 (1.10.1 RVA 0x52EB70)
		private void Awake()
		{
			UpdateCachedValues();
		}

		// 06000523 (1.10.1 RVA 0x52ED00)
		private void OnValidate()
		{
			if (gameObject.CanValidate())
				UpdateCachedValues();
		}

		// 06000524 (1.10.1 RVA 0x52EB80): a repeated event string keeps the last action.
		private void UpdateCachedValues()
		{
			m_indexedActions.Clear();
			if (m_animatorEventActions == null)
				return;
			foreach (AnimationEventAction eventAction in m_animatorEventActions)
				m_indexedActions[eventAction.EventString] = eventAction.Action;
		}

		// 06000525 (1.10.1 RVA 0x52ED40)
		public void Action(AnimationEvent animationEvent)
		{
			if (m_indexedActions.TryGetValue(animationEvent.stringParameter, out UnityEvent action))
				action.Invoke();
		}

		// 06000526 (1.10.1 RVA 0x52EDC0)
		public AnimationEventListener()
		{
		}
	}
}
