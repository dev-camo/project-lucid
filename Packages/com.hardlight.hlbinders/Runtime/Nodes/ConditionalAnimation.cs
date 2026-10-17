using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200001f; bodies translated from the 1.10.1 x86-64 binary.
	[AddNodeMenu("Conditional/Animation", int.MaxValue)]
	[AddComponentMenu("")]
	public class ConditionalAnimation : Conditional<RuntimeAnimatorController>
	{
		// 06000096 (1.10.1 RVA 0x19FA900)
		public ConditionalAnimation()
		{
		}
	}
}
