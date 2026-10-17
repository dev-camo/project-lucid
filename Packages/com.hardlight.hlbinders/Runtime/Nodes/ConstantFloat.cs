using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000033; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Constants/Float", int.MaxValue)]
	public class ConstantFloat : Constant<float>
	{
		// 060000ae (1.10.1 RVA 0x19FADC0)
		public ConstantFloat()
		{
		}
	}
}
