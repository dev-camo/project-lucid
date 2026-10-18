using TMPro;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200005a; bodies translated from the 1.10.1 x86-64 binary.
	[AddNodeMenu("Logic/Switch-TMP_Gradient", int.MaxValue)]
	[AddComponentMenu("")]
	public sealed class LogicalSwitchTMPGradient : LogicalSwitch<TMP_ColorGradient>
	{
		// 06000111 (1.10.1 RVA 0x1A014C0)
		public LogicalSwitchTMPGradient()
		{
		}
	}
}
