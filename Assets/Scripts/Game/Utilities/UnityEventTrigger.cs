using System;
using System.Collections;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
	// Original Game.Runtime 0x02000a3d; bodies translated from the 1.10.1 x86-64 binary.
	// UnityEventParameters is declared first, which is what numbers DelayCoroutine's iterator d__11.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class UnityEventTrigger : MonoBehaviour
	{
		[Serializable]
		private class UnityEventParameters
		{
			public UnityEvent TriggerEvent;
			public float DelaySeconds;
			public TimeCategory DelayCategory = TimeCategory.System;
		}

		[SerializeField]
		private UnityEventParameters m_onStart;
		[SerializeField]
		private UnityEventParameters m_onEnabled;
		[SerializeField]
		private UnityEventParameters m_onDisabled;
		[SerializeField]
		private UnityEventParameters m_onDestroy;
		private readonly SystemRef<CoroutineUtils> m_coroutineSystem = ProcessManager.GetSystemRef<CoroutineUtils>(null, true);

		// 06003a8e (1.10.1 RVA 0x6B94A0)
		private void Start()
		{
			TriggerEvent(m_onStart);
		}

		// 06003a8f (1.10.1 RVA 0x6B9580)
		private void OnEnable()
		{
			TriggerEvent(m_onEnabled);
		}

		// 06003a90 (1.10.1 RVA 0x6B9590)
		private void OnDisable()
		{
			TriggerEvent(m_onDisabled);
		}

		// 06003a91 (1.10.1 RVA 0x6B95A0)
		private void OnDestroy()
		{
			TriggerEvent(m_onDestroy);
		}

		// 06003a92 (1.10.1 RVA 0x6B94B0): a zero delay raises the event at once; any other delay
		// is run on the CoroutineUtils system and is dropped while that system is absent.
		private void TriggerEvent(UnityEventParameters eventParameters)
		{
			if (eventParameters.DelaySeconds == 0f)
			{
				eventParameters.TriggerEvent.Invoke();
				return;
			}
			if (m_coroutineSystem.IsNull())
				return;
			CoroutineUtils.RunCoroutine(DelayCoroutine(eventParameters));
		}

		// 06003a93 (1.10.1 RVA 0x6B95B0) with the iterator 06003a96..9b
		private static IEnumerator DelayCoroutine(UnityEventParameters eventParameters)
		{
			yield return TimeScaledUtilities_SDT.WaitForFixedSeconds(eventParameters.DelaySeconds, eventParameters.DelayCategory);
			eventParameters.TriggerEvent.Invoke();
		}
	}
}
