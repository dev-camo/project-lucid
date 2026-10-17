using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLUnityUI.Runtime 0x020000b5 (TypeDefIndex 12005); x86-64 bodies.
	// Reference-counted: the overlay is shown by the first open and hidden when the
	// count is back at zero. A close with nothing open still hides it.
	[AddComponentMenu("Hardlight UI/Navigation/Open-Close Overlay Page")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class OpenCloseOverlayPage : MonoBehaviour
	{
		private int refCount;
		[SerializeField]
		protected OverlayPageIdentifier m_pageToOpenAndClose;

		// 0x0600051f (0x1b633e0)
		public void OpenPage()
		{
			if (m_pageToOpenAndClose == OverlayPageIdentifier.None)
			{
				return;
			}
			if (refCount == 0)
			{
				OverlaySystem.Instance.ShowPage(m_pageToOpenAndClose);
			}
			refCount++;
		}

		// 0x06000520 (0x1b63460)
		public void ClosePage()
		{
			if (m_pageToOpenAndClose == OverlayPageIdentifier.None)
			{
				return;
			}
			if (refCount > 0)
			{
				refCount--;
			}
			if (refCount == 0)
			{
				OverlaySystem.Instance.HidePage(m_pageToOpenAndClose);
			}
		}

		// 0x06000521 (0x1b634e0)
		public OpenCloseOverlayPage()
		{
		}
	}
}
