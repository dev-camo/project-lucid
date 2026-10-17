using System;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000023; bodies translated from the 1.10.1 x86-64 binary.
	[AddNodeMenu("Conditional/DateTime", int.MaxValue)]
	[AddComponentMenu("")]
	public class ConditionalDateTime : Conditional<DateTime>
	{
		// 0600009a (1.10.1 RVA 0x19FAA00)
		public ConditionalDateTime()
		{
		}
	}
}
