using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200006e; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Conditional/Number Conditional Colour", int.MaxValue)]
	public class NumberConditionalColour : NumberConditional<Color>
	{
		// 0600014f (1.10.1 RVA 0x1A065F0)
		public NumberConditionalColour()
		{
		}
	}
}
