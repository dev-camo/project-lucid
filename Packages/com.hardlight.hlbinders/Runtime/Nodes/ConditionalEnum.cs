using System;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000024; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Conditional/Enum", int.MaxValue)]
	public class ConditionalEnum : Conditional<Enum>
	{
		// 0600009b (1.10.1 RVA 0x19FAA40)
		public ConditionalEnum()
		{
		}
	}
}
