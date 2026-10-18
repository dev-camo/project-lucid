using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000442; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class GlobalSignalPublisher : MonoBehaviour
	{
		[SerializeField]
		private GlobalSignal m_globalSignal;
		private readonly SystemRef<MessageManager> m_messageManagerRef = ProcessManager.GetSystemRef<MessageManager>();

		// 06001937 (1.10.1 RVA 0x7A4CB0): without a message manager the signal is dropped.
		public void Raise()
		{
			if (m_messageManagerRef.TryGet(out MessageManager messageManager))
				messageManager.ScriptableObjectMessagesWithoutCompletion.PublishMessage(m_globalSignal);
		}
	}
}
