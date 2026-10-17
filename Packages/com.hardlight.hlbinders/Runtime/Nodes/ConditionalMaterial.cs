using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000028; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Conditional/Material", int.MaxValue)]
	public class ConditionalMaterial : Conditional<Material>
	{
		// 0600009f (1.10.1 RVA 0x19FAB40)
		public ConditionalMaterial()
		{
		}
	}
}
