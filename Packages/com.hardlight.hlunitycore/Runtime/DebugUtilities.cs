using Unity.IL2CPP.CompilerServices;
using System;
using System.Diagnostics;

// Original HLUnityCore.Runtime 02000003; bodies translated from the 1.10.1 x86-64 binary.
[Il2CppSetOption(Option.NullChecks, false)]
[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
public sealed class DebugUtilities
{
	// 06000002 (1.10.1 RVA 0x1A98900)
	[Conditional("BUILD_DEVELOPMENT")]
	public static void ConditionalAction(bool condition, Action action)
    {
        if (condition)
            action();
    }

	// 06000003 (1.10.1 RVA 0x1A98920)
	public DebugUtilities()
    {
    }
}
