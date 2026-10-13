using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 0200098f; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class SimpleStringMessageData
	{
		public readonly string TextString;

		// 06003744 (1.10.1 RVA 0x696A30)
		public SimpleStringMessageData()
		{
			TextString = string.Empty;
		}

		// 06003745 (1.10.1 RVA 0x696A90)
		public SimpleStringMessageData(string textString)
		{
			TextString = textString;
		}
	}
}
