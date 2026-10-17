using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200002a; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Conditional/String", int.MaxValue)]
	public class ConditionalString : Conditional<string>
	{
		// 060000a1 (1.10.1 RVA 0x19FABC0)
		public ConditionalString()
		{
		}
	}
}
