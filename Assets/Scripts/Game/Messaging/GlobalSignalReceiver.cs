using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
	// Original Game.Runtime 02000443; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class GlobalSignalReceiver : MonoBehaviour
	{
		[SerializeField]
		private GlobalSignal m_signal;
		[SerializeField]
		private UnityEvent m_response;
		private readonly SystemRef<MessageManager> m_messageManagerRef = ProcessManager.GetSystemRef<MessageManager>();

		// 06001939 (1.10.1 RVA 0x7A4DC0); the lambda is <OnEnable>b__3_0 (0x7A4FB0). The
		// subscription waits for the message manager and its handle is not kept.
		private void OnEnable()
		{
			m_messageManagerRef.InvokeOnValid(messageManager =>
				messageManager.ScriptableObjectMessagesWithoutCompletion.SubscribeToMessage(m_signal, OnSignalReceived));
		}

		// 0600193a (1.10.1 RVA 0x7A4E50): a subscription still deferred on the reference is
		// not withdrawn.
		private void OnDisable()
		{
			if (m_messageManagerRef.TryGet(out MessageManager messageManager))
				messageManager.ScriptableObjectMessagesWithoutCompletion.UnsubscribeFromMessage(m_signal, OnSignalReceived);
		}

		// 0600193b (1.10.1 RVA 0x7A4F20)
		private void OnSignalReceived()
		{
			m_response.Invoke();
		}
	}
}
