using Unity.IL2CPP.CompilerServices;

namespace Hardlight.AutoPlay
{
	// Original HLAutoPlay.Runtime 02000003. The shipped build keeps only the property's
	// backing field; its accessors were stripped, so their access is not recovered.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class AutoPlayScript
	{
		public IPerformanceTool PerformanceTool { get; set; }
	}
}
