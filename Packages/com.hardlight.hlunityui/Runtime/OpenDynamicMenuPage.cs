using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLUnityUI.Runtime 0x020000b8 (TypeDefIndex 12008); x86-64 bodies.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[AddComponentMenu("Hardlight UI/Navigation/Open Dynamic Menu Page")]
	public class OpenDynamicMenuPage : MonoBehaviour
	{
		[SerializeField]
		protected MenuPageIdentifier m_pageToOpen;
		[SerializeField]
		protected bool m_replaceCurrent;
		[SerializeField]
		protected DynamicPageEntry m_dynamicPageChoice;

		// 0x06000524 (0x1b635f0): the parameters are built before the page is checked.
		public void OpenPage()
		{
			DynamicPageLoadParameters parameters = new DynamicPageLoadParameters();
			parameters.m_pageEntry = m_dynamicPageChoice;
			if (m_pageToOpen != MenuPageIdentifier.None)
			{
				MenuSystem.Instance.MoveToPageWithData(m_pageToOpen, m_replaceCurrent, parameters);
			}
		}

		// 0x06000525 (0x1b636a0)
		public OpenDynamicMenuPage()
		{
		}
	}
}
