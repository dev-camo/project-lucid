using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000026; bodies translated from the 1.10.1 x86-64 binary.
	[AddNodeMenu("Conditional/Int", int.MaxValue)]
	[AddComponentMenu("")]
	public class ConditionalInt : Conditional<int>
	{
		// 0600009d (1.10.1 RVA 0x19FAAC0)
		public ConditionalInt()
		{
		}
	}
}
