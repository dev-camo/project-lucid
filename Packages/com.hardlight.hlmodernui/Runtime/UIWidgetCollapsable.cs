using Hardlight.Enums;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLModernUI.Runtime 02000045; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class UIWidgetCollapsable : MonoBehaviour
	{
		[SerializeField]
		private RectTransform m_collapsableContent;

		[SerializeField]
		private UIWidgetButtonLocalised m_collapsableButton;

		[SerializeField]
		private Strings m_openString;

		[SerializeField]
		private Strings m_closeString;

		private float m_originalHeight;

		private bool m_isOpen;

		// 060001c1 (1.10.1 RVA 0x1A49740)
		private void Start()
		{
			m_originalHeight = m_collapsableContent.sizeDelta.y;
		}

		// 060001c2 (1.10.1 RVA 0x1A49770): the label is read at 0x30 + 4 * the pre-toggle
		// m_isOpen, so a widget that is now open shows m_openString.
		public void Action_OnClick()
		{
			m_isOpen = !m_isOpen;
			m_collapsableButton.SetLabel(m_isOpen ? m_openString : m_closeString);
			m_collapsableContent.sizeDelta = new Vector2(m_collapsableContent.sizeDelta.x, m_isOpen ? m_originalHeight : 0f);
		}

		// 060001c3 (1.10.1 RVA 0x1A49830)
		public UIWidgetCollapsable()
		{
		}
	}
}
