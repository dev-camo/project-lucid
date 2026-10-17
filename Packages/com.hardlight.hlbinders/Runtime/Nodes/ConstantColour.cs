using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000032; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Constants/Colour", int.MaxValue)]
	public class ConstantColour : Constant<Color>
	{
		// 060000ad (1.10.1 RVA 0x19FAD80)
		public ConstantColour()
		{
		}
	}
}
