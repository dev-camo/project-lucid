using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
	// Original HLUnityCore.Runtime 02000043; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public static class HLProfilerEnumComparers
	{
		// 040000b6; initialised by the original static constructor 06000199 (1.10.1 RVA 0x1AA2A90).
		public static readonly HLProfilerCategoryEqualityComparer CategoryComparer = new HLProfilerCategoryEqualityComparer();
	}
}
