using System.Collections.Generic;
using System.Diagnostics;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// HLUnityCore.Runtime TypeDefIndex 9128 (0x02000179), methods 0x06000a88..0x06000a8b;
	// bodies from the 1.10.1 x86-64 binary (0x1b03b30..0x1b03ca0). The type is
	// beforefieldinit, so the stack is a field initializer (.cctor 0x1b03ca0).
	// Begin and End are [Conditional("BUILD_DEVELOPMENT")]. Inside them a call to a
	// [Conditional] member that consumed the message (and in End possibly the popped
	// start time) was compiled out of the retail bodies; its target is not
	// recoverable. Only the stack bookkeeping, the message emptiness test and the
	// discarded Pop survive, and the removed calls are not reconstructed.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class HLProfile
	{
		private static Stack<float> m_profileTimeStack = new Stack<float>();

		// 0x06000a88, 0x1b03b30: the start time is pushed before the message test,
		// whose guarded body is empty in this build.
		[Conditional("BUILD_DEVELOPMENT")]
		public static void Begin(string message = null)
		{
			m_profileTimeStack.Push(Time.realtimeSinceStartup);
			if (!string.IsNullOrEmpty(message))
			{
				// guarded [Conditional] call compiled out of the retail body; target unrecoverable
			}
		}

		// 0x06000a89, 0x1b03bb0: an unmatched End is ignored; otherwise the start time is
		// popped and dropped. The message is not read.
		[Conditional("BUILD_DEVELOPMENT")]
		public static void End(string message)
		{
			if (m_profileTimeStack.Count != 0)
			{
				m_profileTimeStack.Pop();
			}
		}
	}
}
