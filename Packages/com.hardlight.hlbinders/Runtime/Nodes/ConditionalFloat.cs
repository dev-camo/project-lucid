using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000025; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Conditional/Float", int.MaxValue)]
	public class ConditionalFloat : Conditional<float>
	{
		// 0600009c (1.10.1 RVA 0x19FAA80)
		public ConditionalFloat()
		{
		}
	}
}
