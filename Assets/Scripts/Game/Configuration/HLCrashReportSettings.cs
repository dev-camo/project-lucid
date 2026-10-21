using Unity.IL2CPP.CompilerServices;
using Hardlight;

namespace HardlightProject
{
	// Original Game.Runtime 02000421; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class HLCrashReportSettings : ICrashReportSettings
	{
		// 0600185a (1.10.1 RVA 0x79B6F0)
		public bool GetServiceEnabled()
		{
			return SystemConfiguration.GetConfig<CoreGameConfiguration>().SendCrashReports;
		}

		// 0600185b (1.10.1 RVA 0x79B730)
		public HLCrashReportSettings()
		{;}
	}
}
