using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace Hardlight
{
	// Original HLModernUI.Runtime 02000043; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[RequireComponent(typeof(Toggle))]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class UIWidgetCheckbox : UIWidget
	{
		private bool m_isOn;

		private Action<bool> m_callback;

		// 060001bd (1.10.1 RVA 0x1A49610)
		public override void Setup(IUIWidgetParameters parameters)
		{
			if (parameters is UIWidgetCheckboxParameters data)
				m_callback = data.Callback;
			else
				HLOutput.LogError("Could not set up UIWidgetCheckbox with given data.");
		}

		// 060001be (1.10.1 RVA 0x1A496D0)
		public void Action_Toggle(bool value)
		{
			m_isOn = value;
			m_callback?.Invoke(value);
		}

		// 060001bf (1.10.1 RVA 0x1A49700)
		public UIWidgetCheckbox()
		{
		}
	}
}
