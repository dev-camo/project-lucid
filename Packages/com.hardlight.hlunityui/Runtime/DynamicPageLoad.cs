using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLUnityUI.Runtime 0x020000b2 (TypeDefIndex 12003); x86-64 bodies.
	// Shows every entry whose dynamic ID matches the page parameters; the last match
	// becomes the active content that OnHide forwards to.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class DynamicPageLoad : GuiPageContent
	{
		// Original 0x020000b3 (TypeDefIndex 12002).
		[Serializable]
		private class PageEnumPairing
		{
			public DynamicPageEntry m_dynamicID;
			public GuiPageContent m_page;

			// 0x0600051b (0x1b632b0)
			public PageEnumPairing()
			{
			}
		}

		[SerializeField]
		private PageEnumPairing[] m_pageEntries;
		private GuiPageContent m_activeContent;

		// 0x06000518 (0x1b63140): the base receives the cast parameters (null for any
		// other type), while each matching entry receives the original ones.
		public override void OnShow(GuiPageParameters loadParameters)
		{
			DynamicPageLoadParameters parameters = loadParameters as DynamicPageLoadParameters;
			base.OnShow(parameters);
			for (int i = 0; i < m_pageEntries.Length; i++)
			{
				if (parameters.m_pageEntry == m_pageEntries[i].m_dynamicID)
				{
					m_pageEntries[i].m_page.gameObject.SetActive(true);
					m_pageEntries[i].m_page.OnShow(loadParameters);
					m_activeContent = m_pageEntries[i].m_page;
				}
			}
		}

		// 0x06000519 (0x1b63260)
		public override void OnHide(GuiPageParameters parameters, UISystemClosureReason reasonForClosure)
		{
			base.OnHide(parameters, reasonForClosure);
			m_activeContent.OnHide(parameters, reasonForClosure);
		}

		// 0x0600051a (0x1b632a0)
		public DynamicPageLoad()
		{
		}
	}
}
