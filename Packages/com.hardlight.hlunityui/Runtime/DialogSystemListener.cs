using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace Hardlight
{
	// Original HLUnityUI.Runtime 0x020000bb (TypeDefIndex 12011); x86-64 bodies.
	[AddComponentMenu("Hardlight UI/Events/Dialog System Listener")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class DialogSystemListener : MonoBehaviour
	{
		[SerializeField]
		protected UnityEvent m_onDialogSystemOpened = new UnityEvent();
		[SerializeField]
		protected UnityEvent m_onDialogSystemClosed = new UnityEvent();

		// 0x0600052a (0x1b63800)
		protected void OnEnable()
		{
			DialogSystem.Instance.OnSystemOpened += DialogSystemOpenedHandler;
			DialogSystem.Instance.OnSystemClosed += DialogSystemClosedHandler;
		}

		// 0x0600052b (0x1b63930)
		protected void OnDisable()
		{
			DialogSystem.Instance.OnSystemOpened -= DialogSystemOpenedHandler;
			DialogSystem.Instance.OnSystemClosed -= DialogSystemClosedHandler;
		}

		// 0x0600052c (0x1b63a60)
		private void DialogSystemOpenedHandler()
		{
			m_onDialogSystemOpened.Invoke();
		}

		// 0x0600052d (0x1b63a70)
		private void DialogSystemClosedHandler()
		{
			m_onDialogSystemClosed.Invoke();
		}

		// 0x0600052e (0x1b63a80)
		public DialogSystemListener()
		{
		}
	}
}
