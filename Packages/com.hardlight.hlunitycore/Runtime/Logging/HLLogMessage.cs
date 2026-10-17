using System;
using UnityEngine;

namespace Hardlight
{
	// Original HLUnityCore.Runtime 0x02000177 (TypeDefIndex 9126); x86-64 bodies.
	[Serializable]
	public struct HLLogMessage
	{
		public LogType Type;
		public string Message;
		public string StackTrace;

		// 0x06000a69 (0x1b011e0): an empty trace counts as no trace.
		public bool HasStackTrace => !string.IsNullOrEmpty(StackTrace);
	}
}
