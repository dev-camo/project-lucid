using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class HLOutputPluginUnsupported : HLOutputPlugin
	{
		public override void InternalSetDevelopmentBuild(bool isDevelopmentBuild)
		{
			// 06000a94 (1.10.1 RVA 0x1B03D80): the flag is ignored; the platform is reported as an error.
			HLOutput.LogError(string.Format("Native logging is not supported in platform {0}.", UnityEngine.Application.platform));
		}

		public override void InternalTestNativeLogError()
		{
            HLOutput.LogError(string.Format("Native logging is not supported in platform {0}.", UnityEngine.Application.platform));
		}
	}
}
