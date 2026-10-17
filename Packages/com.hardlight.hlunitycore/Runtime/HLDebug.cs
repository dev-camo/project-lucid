using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
	// HLUnityCore.Runtime TypeDefIndex 9115 (0x0200016c), methods 0x060009e9..0x060009eb;
	// bodies from the 1.10.1 x86-64 binary (0x1af8770..0x1af88d0).
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public static class HLDebug
	{
		public const string DebugConditional = "BUILD_DEVELOPMENT";

		public const string EditorConditional = "UNITY_EDITOR";

		// 0x060009e9, 0x1af8770: each unit starts strictly above the previous one (1024
		// bytes is still "1024B"). Larger units divide as float and format with "F",
		// using the current culture's default number of decimal digits and its decimal
		// separator; bytes use the plain long ToString().
		public static string FormatMemorySize(long sizeInBytes)
		{
			if (sizeInBytes > 1073741824)
			{
				return (sizeInBytes / 1073741824f).ToString("F") + "GB";
			}
			if (sizeInBytes > 1048576)
			{
				return (sizeInBytes / 1048576f).ToString("F") + "MB";
			}
			if (sizeInBytes > 1024)
			{
				return (sizeInBytes / 1024f).ToString("F") + "KB";
			}
			return sizeInBytes.ToString() + "B";
		}

		// 0x060009ea, 0x1af88c0: the retail body returns the constant false. Any
		// UNITY_EDITOR branch the source had is not visible in the player build and is
		// not reconstructed.
		public static bool IsEditor()
		{
			return false;
		}

		// 0x060009eb, 0x1af88d0: the retail body returns the constant false (the player
		// was built without BUILD_DEVELOPMENT).
		public static bool IsDevelopmentBuild()
		{
			return false;
		}
	}
}
