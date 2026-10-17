using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000020; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Conditional/AudioClip", int.MaxValue)]
	public class ConditionalAudio : Conditional<AudioClip>
	{
		// 06000097 (1.10.1 RVA 0x19FA940)
		public ConditionalAudio()
		{
		}
	}
}
