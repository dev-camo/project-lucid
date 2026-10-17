using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000029; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Conditional/Sprite", int.MaxValue)]
	public class ConditionalSprite : Conditional<Sprite>
	{
		// 060000a0 (1.10.1 RVA 0x19FAB80)
		public ConditionalSprite()
		{
		}
	}
}
