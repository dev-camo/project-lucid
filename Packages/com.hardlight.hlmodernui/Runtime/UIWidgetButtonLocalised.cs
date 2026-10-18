using Hardlight.Enums;
using Hardlight.Localisation;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
	// Original HLModernUI.Runtime 02000042; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class UIWidgetButtonLocalised : UIWidgetButton
	{
		// 060001bb (1.10.1 RVA 0x1A49590)
		public void SetLabel(Strings identifier)
		{
			Label.Value = StringTable.GetString(identifier);
		}

		// 060001bc (1.10.1 RVA 0x1A49600)
		public UIWidgetButtonLocalised()
		{
		}
	}
}
