using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000057; bodies translated from the 1.10.1 x86-64 binary.
	[AddNodeMenu("Logic/Switch-Int", int.MaxValue)]
	[AddComponentMenu("")]
	public sealed class LogicalSwitchInt : LogicalSwitch<int>
	{
		// 0600010e (1.10.1 RVA 0x1A01400)
		public LogicalSwitchInt()
		{
		}
	}
}
