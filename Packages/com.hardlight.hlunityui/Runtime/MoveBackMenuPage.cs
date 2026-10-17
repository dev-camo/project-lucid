using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLUnityUI.Runtime 0x020000b4 (TypeDefIndex 12004); x86-64 bodies.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class MoveBackMenuPage : MonoBehaviour
	{
		[SerializeField]
		protected MenuPageIdentifier m_defaultPage;

		// 0x0600051c (0x1b632c0): the default page is used only when there is nothing
		// to move back to, and it replaces the current page. A page that refuses to be
		// left blocks the move without falling back.
		public void OpenPage()
		{
			if (MenuSystem.Instance.CanMoveBack)
			{
				if (MenuSystem.Instance.CanLeaveCurrentPage())
				{
					MenuSystem.Instance.MoveToPreviousPage();
				}
			}
			else if (m_defaultPage != MenuPageIdentifier.None)
			{
				MenuSystem.Instance.MoveToPage(m_defaultPage, true);
			}
		}

		// 0x0600051d (0x1b633c0)
		public MenuPageIdentifier GetDefaultPage()
		{
			return m_defaultPage;
		}

		// 0x0600051e (0x1b633d0)
		public MoveBackMenuPage()
		{
		}
	}
}
