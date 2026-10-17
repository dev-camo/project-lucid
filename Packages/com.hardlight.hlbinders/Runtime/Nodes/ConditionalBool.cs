using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000021; bodies translated from the 1.10.1 x86-64 binary.
	[AddNodeMenu("Conditional/Bool", int.MaxValue)]
	[AddComponentMenu("")]
	public class ConditionalBool : Conditional<bool>
	{
		// 06000098 (1.10.1 RVA 0x19FA980)
		public ConditionalBool()
		{
		}
	}
}
