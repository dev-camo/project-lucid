using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
	// Original Game.Runtime 020000ac; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class ApplicationFSMBoolEventTrigger : MonoBehaviour
	{
		[SerializeField]
		private string m_applicationFSMKey;

		[SerializeField]
		private UnityEvent m_eventOnTrue;

		[SerializeField]
		private UnityEvent m_eventOnFalse;

		// 06000592 (1.10.1 RVA 0x534E40): a missing key fires neither event.
		private void Awake()
		{
			GraphStorageKey key = new GraphStorageKey(m_applicationFSMKey, string.Empty, null);
			if (ProcessManager.GetSystem<App>().Storage.TryGetValue(key, out bool value))
			{
				if (value)
				{
					m_eventOnTrue?.Invoke();
				}
				else
				{
					m_eventOnFalse?.Invoke();
				}
			}
		}
	}
}
