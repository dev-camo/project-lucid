using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000059; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Logic/Switch-Sprite", int.MaxValue)]
	public sealed class LogicalSwitchSprite : LogicalSwitch<Sprite>
	{
		// 06000110 (1.10.1 RVA 0x1A01480)
		public LogicalSwitchSprite()
		{
		}
	}
}
