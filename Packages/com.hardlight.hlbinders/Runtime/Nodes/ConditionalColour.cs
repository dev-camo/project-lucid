using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000022; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Conditional/Colour", int.MaxValue)]
	public class ConditionalColour : Conditional<Color>
	{
		// 06000099 (1.10.1 RVA 0x19FA9C0)
		public ConditionalColour()
		{
		}
	}
}
