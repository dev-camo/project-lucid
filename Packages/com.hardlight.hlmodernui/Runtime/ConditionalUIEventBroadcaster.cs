using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLModernUI.Runtime 0200000e; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ConditionalUIEventBroadcaster : MonoBehaviour
	{
		[SerializeField]
		private UIModernEvent m_eventPositive;

		[SerializeField]
		private UIModernEvent m_eventNegative;

		// 06000031 (1.10.1 RVA 0x1A3B270): a missing event publishes nothing and never
		// auto-creates the exchange.
		public void Action_ConditionalEvent(bool toggleState)
		{
			UIModernEvent eventId = toggleState ? m_eventPositive : m_eventNegative;
			if (eventId == null)
				return;
			var exchange = ProcessManager.GetSystemAutoCreate<MessageExchangeBoundCallbackArg<UIModernMessage>>();
			var message = new UIModernMessage(UIModernEventType.OnClick);
			exchange.PublishMessage(in message, in eventId, MessageBroadcastType.Both);
		}

		// 06000032 (1.10.1 RVA 0x1A3B330)
		public ConditionalUIEventBroadcaster()
		{
		}
	}
}
