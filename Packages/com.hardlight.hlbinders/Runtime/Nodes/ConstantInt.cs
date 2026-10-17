using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000034; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Constants/Integer", int.MaxValue)]
	public class ConstantInt : Constant<int>
	{
		// 060000af (1.10.1 RVA 0x19FAE00)
		public ConstantInt()
		{
		}
	}
}
