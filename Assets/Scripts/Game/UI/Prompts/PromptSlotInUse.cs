using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class PromptSlotInUse : MonoBehaviour
	{
		[SerializeField]
		private PromptSlotType m_slotType;

		[SerializeField]
		private UnityEvent m_onPromptSlotInUse;

		[SerializeField]
		private UnityEvent m_onPromptSlotNotInUse;

		private PromptManager m_promptManager;

		private bool m_slotIsInUse;

		private void Awake()
		{
			ProcessManager.GetSystemRef<PromptManager>().InvokeOnValid(RegisterPromptManager);
		}

		private void RegisterPromptManager(PromptManager promptManager)
		{
			m_promptManager = promptManager;
			promptManager.OnInUsePromptSlotTypesUpdated -= OnInUsePromptSlotTypesUpdated;
			promptManager.OnInUsePromptSlotTypesUpdated += OnInUsePromptSlotTypesUpdated;
			Refresh(true);
		}

		private void OnDestroy()
		{
			if (m_promptManager != null)
				m_promptManager.OnInUsePromptSlotTypesUpdated -= OnInUsePromptSlotTypesUpdated;
		}

		private void OnInUsePromptSlotTypesUpdated()
		{
			Refresh();
		}

		private void Refresh(bool force = false)
		{
			bool slotIsInUse = m_promptManager.IsSlotTypeInUse(m_slotType);
			if (slotIsInUse != m_slotIsInUse || force)
			{
				m_slotIsInUse = slotIsInUse;
				if (slotIsInUse)
					m_onPromptSlotInUse?.Invoke();
				else
					m_onPromptSlotNotInUse?.Invoke();
			}
		}
	}
}
