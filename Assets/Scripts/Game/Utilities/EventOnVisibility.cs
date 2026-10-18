using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
	// Original Game.Runtime 02000a04; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[RequireComponent(typeof(Renderer))]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class EventOnVisibility : MonoBehaviour
	{
		[SerializeField]
		private UnityEvent m_onVisibleEvent;
		[SerializeField]
		private UnityEvent m_onInvisibleEvent;

		// 06003979 (1.10.1 RVA 0x6B0990)
		public UnityEvent OnVisibleEvent => m_onVisibleEvent;

		// 0600397a (1.10.1 RVA 0x6B09A0)
		public UnityEvent OnInvisibleEvent => m_onInvisibleEvent;

		// 0600397b/0600397c (1.10.1 RVA 0x6B09B0/0x6B09C0)
		public bool IsVisible { get; private set; }

		// 0600397d (1.10.1 RVA 0x6B09D0): the flag is published before the event runs.
		private void OnBecameVisible()
		{
			IsVisible = true;
			m_onVisibleEvent.Invoke();
		}

		// 0600397e (1.10.1 RVA 0x6B09F0)
		private void OnBecameInvisible()
		{
			IsVisible = false;
			m_onInvisibleEvent.Invoke();
		}
	}
}
