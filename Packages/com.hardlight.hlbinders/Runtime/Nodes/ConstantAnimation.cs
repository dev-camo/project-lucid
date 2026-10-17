using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000030; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Constants/Animation", int.MaxValue)]
	public class ConstantAnimation : Constant<RuntimeAnimatorController>
	{
		// 060000ab (1.10.1 RVA 0x19FAD00)
		public ConstantAnimation()
		{
		}
	}
}
