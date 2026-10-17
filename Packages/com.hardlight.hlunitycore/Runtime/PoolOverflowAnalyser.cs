using System;

namespace Hardlight.Pooling
{
	// Original HLUnityCore.Runtime 02000292; constants from the 1.10.1 metadata.
	public static class PoolOverflowAnalyser
	{
		public const string SnapshotHeadMarker = "[P]";

		public const string PoolElementMarker = "(P)";

		public const string PoolOverflowMarker = "(P>";

		public const char IDSeparator = '|';
	}
}
