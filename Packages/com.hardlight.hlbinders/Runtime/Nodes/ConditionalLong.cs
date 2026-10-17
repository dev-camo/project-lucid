using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000027; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Conditional/Long", int.MaxValue)]
	public class ConditionalLong : Conditional<long>
	{
		// 0600009e (1.10.1 RVA 0x19FAB00)
		public ConditionalLong()
		{
		}
	}
}
