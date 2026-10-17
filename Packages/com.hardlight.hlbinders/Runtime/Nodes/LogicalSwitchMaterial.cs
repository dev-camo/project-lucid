using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000058; bodies translated from the 1.10.1 x86-64 binary.
	[AddNodeMenu("Logic/Switch-Material", int.MaxValue)]
	[AddComponentMenu("")]
	public sealed class LogicalSwitchMaterial : LogicalSwitch<Material>
	{
		// 0600010f (1.10.1 RVA 0x1A01440)
		public LogicalSwitchMaterial()
		{
		}
	}
}
