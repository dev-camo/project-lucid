using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000035; bodies translated from the 1.10.1 x86-64 binary.
	[AddNodeMenu("Constants/Long Integer", int.MaxValue)]
	[AddComponentMenu("")]
	public class ConstantLong : Constant<long>
	{
		// 060000b0 (1.10.1 RVA 0x19FAE40)
		public ConstantLong()
		{
		}
	}
}
