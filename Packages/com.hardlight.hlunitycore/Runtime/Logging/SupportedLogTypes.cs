using System;

namespace Hardlight
{
	// Original HLUnityCore.Runtime 02000171; recovered enum declaration from the 1.10.1 metadata (no own methods).
	[Flags]
	public enum SupportedLogTypes
	{
		Log = 2,
		Warning = 4,
		Error = 8,
		Assert = 0x10,
		Exception = 0x20
	}
}
