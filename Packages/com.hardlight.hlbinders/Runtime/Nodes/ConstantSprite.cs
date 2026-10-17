using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000038; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Constants/Sprite", int.MaxValue)]
	public class ConstantSprite : Constant<Sprite>
	{
		// 060000b4 (1.10.1 RVA 0x19FAFB0)
		public ConstantSprite()
		{
		}
	}
}
