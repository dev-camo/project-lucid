using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200002d; bodies translated from the 1.10.1 x86-64 binary.
	[AddNodeMenu("Conditional/Vector2", int.MaxValue)]
	[AddComponentMenu("")]
	public class ConditionalVector2 : Conditional<Vector2>
	{
		// 060000a4 (1.10.1 RVA 0x19FAC80)
		public ConditionalVector2()
		{
		}
	}
}
