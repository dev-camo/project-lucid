using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLUnityUI.Runtime 0x020000b9 (TypeDefIndex 12009); x86-64 bodies.
	[AddComponentMenu("Hardlight UI/Navigation/Open Menu Page")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class OpenMenuPage : MonoBehaviour
	{
		[SerializeField]
		protected MenuPageIdentifier m_pageToOpen;
		[SerializeField]
		protected bool m_replaceCurrent;

		// 0x06000526 (0x1b636b0)
		public void OpenPage()
		{
			if (m_pageToOpen != MenuPageIdentifier.None)
			{
				MenuSystem.Instance.MoveToPage(m_pageToOpen, m_replaceCurrent);
			}
		}

		// 0x06000527 (0x1b63720)
		public OpenMenuPage()
		{
		}
	}
}
