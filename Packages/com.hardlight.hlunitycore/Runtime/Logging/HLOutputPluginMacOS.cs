using System;
using System.Runtime.InteropServices;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class HLOutputPluginMacOS : HLOutputPlugin
	{
#pragma warning disable 0626 // The import module was not recovered; offline, hooks H-062 and H-063 never call these.
		[PreserveSig]
		private static extern void SetDevelopmentBuild(bool isDevelopmentBuild);

		[PreserveSig]
		private static extern void Unity_TestNativeLogError();
#pragma warning restore 0626

		public override void InternalSetDevelopmentBuild(bool isDevelopmentBuild)
		{
			if (ProjectLucid.Offline.OfflineAdapters.Active) return; // Offline hook H-062: the original passed the flag to the SetDevelopmentBuild native import; there is no native logger offline
			SetDevelopmentBuild(isDevelopmentBuild);
		}

		public override void InternalTestNativeLogError()
		{
			if (ProjectLucid.Offline.OfflineAdapters.Active) throw new NotSupportedException("The studio native log-error test has no offline equivalent."); // Offline hook H-063: the original called the Unity_TestNativeLogError native import
			Unity_TestNativeLogError();
		}
	}
}
