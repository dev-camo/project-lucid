using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class HLOutputPluginUnsupported : HLOutputPlugin
	{
		public override void InternalSetDevelopmentBuild(bool isDevelopmentBuild)
		{
		}

		public override void InternalTestNativeLogError()
		{
            HLOutput.LogError(string.Format("Native logging is not supported in platform {0}.", UnityEngine.Application.platform));
		}
	}
}
