using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Hardlight
{
	// Original HLModernUI.Runtime 02000048; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class UIWidgetRadioButtonGroup : MonoBehaviour
	{
		[SerializeField]
		private UIWidgetRadioButtonGroupSelection m_selection;

		private UIWidgetRadioButton[] m_childRadioButtons;

		// 060001cc (1.10.1 RVA 0x1A499C0)
		private void Start()
		{
			m_childRadioButtons = GetComponentsInChildren<UIWidgetRadioButton>();
			for (int i = 0; i < m_childRadioButtons.Length; i++)
			{
				var btn = m_childRadioButtons[i];
				btn.OnToggle += OnRadioButtonToggle;
			}
		}

		// 060001cd (1.10.1 RVA 0x1A49B10)
		private void OnRadioButtonToggle(UIWidgetRadioButton radioButton)
		{
			if (m_selection == UIWidgetRadioButtonGroupSelection.Single)
			{
				DoSingleToggle(radioButton);
			}
		}

		// 060001ce (1.10.1 RVA 0x1A49BD0)
		private void DoSingleToggle(UIWidgetRadioButton radioButton)
		{
			for (int i = 0; i < m_childRadioButtons.Length; i++)
			{
				var btn = m_childRadioButtons[i];
				// Unity equality selects the incoming button, not the child (0x1A49C71).
				if (btn == radioButton) radioButton.Select(); else btn.Deselect();
			}
		}

		// 060001cf (1.10.1 RVA 0x1A49C90)
		private void DoMultipleToggle()
		{
		}

		// 060001d0 (1.10.1 RVA 0x1A49CA0)
		public UIWidgetRadioButtonGroup()
		{
		}
	}
}
