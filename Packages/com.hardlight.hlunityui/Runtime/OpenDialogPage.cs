using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLUnityUI.Runtime 0x020000b6 (TypeDefIndex 12007); x86-64 bodies.
	[AddComponentMenu("Hardlight UI/Navigation/Open Dialog Page")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class OpenDialogPage : MonoBehaviour
	{
		// Original 0x020000b7 (TypeDefIndex 12006).
		public enum OpeningBehaviour
		{
			Immediate = 0,
			Enqueue = 1,
			EnqueueAfterCurrent = 2
		}

		[SerializeField]
		protected DialogPageIdentifier m_pageToOpen;
		[SerializeField]
		protected OpeningBehaviour m_openingBehaviour;

		// 0x06000522 (0x1b634f0): an unknown behaviour opens nothing.
		public void OpenPage()
		{
			if (m_pageToOpen == DialogPageIdentifier.None)
			{
				return;
			}
			switch (m_openingBehaviour)
			{
				case OpeningBehaviour.Immediate:
					DialogSystem.Instance.ShowPage(m_pageToOpen);
					break;
				case OpeningBehaviour.Enqueue:
					DialogSystem.Instance.EnqueuePage(m_pageToOpen);
					break;
				case OpeningBehaviour.EnqueueAfterCurrent:
					DialogSystem.Instance.EnqueuePageAfterCurrent(m_pageToOpen);
					break;
			}
		}

		// 0x06000523 (0x1b635e0)
		public OpenDialogPage()
		{
		}
	}
}
