using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000036; bodies translated from the 1.10.1 x86-64 binary.
	[AddNodeMenu("Constants/Material", int.MaxValue)]
	[AddComponentMenu("")]
	public class ConstantMaterial : Constant<Material>
	{
		// 060000b1 (1.10.1 RVA 0x19FAE80)
		public ConstantMaterial()
		{
		}
	}
}
