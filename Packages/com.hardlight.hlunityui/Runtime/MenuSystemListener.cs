using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace Hardlight
{
	// Original HLUnityUI.Runtime 0x020000bc (TypeDefIndex 12012); x86-64 bodies.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[AddComponentMenu("Hardlight UI/Events/Menu System Listener")]
	public class MenuSystemListener : MonoBehaviour
	{
		[SerializeField]
		protected UnityEvent m_onMenuSystemOpened = new UnityEvent();
		[SerializeField]
		protected UnityEvent m_onMenuSystemClosed = new UnityEvent();

		// 0x0600052f (0x1b63b10)
		protected void OnEnable()
		{
			MenuSystem.Instance.OnSystemOpened += MenuSystemOpenedHandler;
			MenuSystem.Instance.OnSystemClosed += MenuSystemClosedHandler;
		}

		// 0x06000530 (0x1b63c40)
		protected void OnDisable()
		{
			MenuSystem.Instance.OnSystemOpened -= MenuSystemOpenedHandler;
			MenuSystem.Instance.OnSystemClosed -= MenuSystemClosedHandler;
		}

		// 0x06000531 (0x1b63d70)
		private void MenuSystemOpenedHandler()
		{
			m_onMenuSystemOpened.Invoke();
		}

		// 0x06000532 (0x1b63d80)
		private void MenuSystemClosedHandler()
		{
			m_onMenuSystemClosed.Invoke();
		}

		// 0x06000533 (0x1b63d90)
		public MenuSystemListener()
		{
		}
	}
}
