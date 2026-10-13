using Unity.IL2CPP.CompilerServices;
using System;
using Hardlight.Enums;
using Hardlight.Localisation;
using HardlightProject;

namespace Hardlight
{
	// Original HLModernUI.Runtime 0200004b; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class UIWidgetToggleParameters : IUIWidgetParameters
	{
		public readonly string Label;

		public readonly Action<bool> Callback;

		// 060001dd (1.10.1 RVA 0x1A49F90)
		public UIWidgetToggleParameters(string label, Action<bool> callback)
		{
			Label = label;
			Callback = callback;
		}

		// 060001de (1.10.1 RVA 0x1A49FE0)
		public UIWidgetToggleParameters(Strings label, Action<bool> callback)
		{
			Label = StringTable.GetString(label);
			Callback = callback;
		}
	}
}
