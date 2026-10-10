using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class PromptSlot : MonoBehaviour
	{
		public enum PromptSlotDirection
		{
			None = 0,
			Up = 1,
			Down = 2,
			Left = 3,
			Right = 4
		}

		[SerializeField]
		private PromptSlotType m_slotType;

		[SerializeField]
		private PromptSlotDirection m_slotDirection;

		public PromptSlotType SlotType => m_slotType;

		public PromptSlotDirection SlotDirection => m_slotDirection;

		private void Awake()
		{
			ProcessManager.GetSystemRef<PromptManager>().InvokeOnValid(RegisterPromptManager);
		}

		private void RegisterPromptManager(PromptManager promptManager)
		{
			promptManager.RegisterPromptSlot(this);
		}

		private void OnDestroy()
		{
			PromptManager promptManager = ProcessManager.GetSystemSafe<PromptManager>();
			if (promptManager != null)
				promptManager.UnregisterPromptSlot(this);
		}
	}
}
