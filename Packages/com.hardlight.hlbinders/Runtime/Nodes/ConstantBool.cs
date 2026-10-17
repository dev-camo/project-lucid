using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000031; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Constants/Boolean", int.MaxValue)]
	public class ConstantBool : Constant<bool>
	{
		// 060000ac (1.10.1 RVA 0x19FAD40)
		public ConstantBool()
		{
		}
	}
}
