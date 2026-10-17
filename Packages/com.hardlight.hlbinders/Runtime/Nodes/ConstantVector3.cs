using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200003d; bodies translated from the 1.10.1 x86-64 binary.
	[AddNodeMenu("Constants/Vector3", int.MaxValue)]
	[AddComponentMenu("")]
	public class ConstantVector3 : Constant<Vector3>
	{
		// 060000c3 (1.10.1 RVA 0x19FB470)
		public ConstantVector3()
		{
		}
	}
}
