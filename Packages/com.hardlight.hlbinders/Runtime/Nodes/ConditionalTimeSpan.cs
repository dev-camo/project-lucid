using System;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200002b; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Conditional/TimeSpan", int.MaxValue)]
	public class ConditionalTimeSpan : Conditional<TimeSpan>
	{
		// 060000a2 (1.10.1 RVA 0x19FAC00)
		public ConditionalTimeSpan()
		{
		}
	}
}
