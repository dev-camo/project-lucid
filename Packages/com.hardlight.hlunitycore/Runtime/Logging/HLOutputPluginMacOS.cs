using System;
using System.Runtime.InteropServices;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	// Two private studio imports are explicit local desktop adaptations;
    // their implementations and changed P/Invoke flags have no original-body credit.
    // The two original virtual forwarding methods and base-only ctor remain original.
    public class HLOutputPluginMacOS : HLOutputPlugin
	{
		[PreserveSig]
		private static void SetDevelopmentBuild(bool isDevelopmentBuild) { }

		[PreserveSig]
		private static void Unity_TestNativeLogError() => throw new NotSupportedException("The original studio native log-error test is unavailable in the local desktop boundary.");

		public override void InternalSetDevelopmentBuild(bool isDevelopmentBuild)
		{
            SetDevelopmentBuild(isDevelopmentBuild);
		}

		public override void InternalTestNativeLogError()
		{
            Unity_TestNativeLogError();
		}
	}
}
