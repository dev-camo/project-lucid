using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLUnityUI.Runtime 0x020000ba (TypeDefIndex 12010); x86-64 bodies.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[AddComponentMenu("Hardlight UI/Navigation/Toggle Overlay Page")]
	public class ToggleOverlayPage : MonoBehaviour
	{
		[SerializeField]
		private OverlayPageIdentifier m_pageToOpenAndClose;

		// 0x06000528 (0x1b63730): unlike OpenCloseOverlayPage, None is not filtered out.
		public void Action_TogglePage()
		{
			if (OverlaySystem.Instance.IsOpen(m_pageToOpenAndClose))
			{
				OverlaySystem.Instance.HidePage(m_pageToOpenAndClose);
			}
			else
			{
				OverlaySystem.Instance.ShowPage(m_pageToOpenAndClose);
			}
		}

		// 0x06000529 (0x1b637f0)
		public ToggleOverlayPage()
		{
		}
	}
}
