using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200002e; bodies translated from the 1.10.1 x86-64 binary.
	[AddNodeMenu("Conditional/Vector3", int.MaxValue)]
	[AddComponentMenu("")]
	public class ConditionalVector3 : Conditional<Vector3>
	{
		// 060000a5 (1.10.1 RVA 0x19FACC0)
		public ConditionalVector3()
		{
		}
	}
}
