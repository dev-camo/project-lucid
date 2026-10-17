using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000056; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Logic/Switch-Colour", int.MaxValue)]
	public sealed class LogicalSwitchColour : LogicalSwitch<Color>
	{
		// 0600010d (1.10.1 RVA 0x1A013C0)
		public LogicalSwitchColour()
		{
		}
	}
}
