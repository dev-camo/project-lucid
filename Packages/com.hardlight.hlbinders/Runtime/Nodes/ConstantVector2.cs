using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200003c; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Constants/Vector2", int.MaxValue)]
	public class ConstantVector2 : Constant<Vector2>
	{
		// 060000c2 (1.10.1 RVA 0x19FB430)
		public ConstantVector2()
		{
		}
	}
}
