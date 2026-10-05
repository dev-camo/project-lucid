using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public abstract class HLOutputPlugin
	{
		public abstract void InternalSetDevelopmentBuild(bool isDevelopmentBuild);

		public abstract void InternalTestNativeLogError();
	}
}
