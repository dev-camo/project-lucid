using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000040; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("DebugLog", int.MaxValue)]
	public class DebugLogNode : BinderNode
	{
		[NodeInput]
		public BindReference First = new BindReference();

		// 060000ca (1.10.1 RVA 0x19FC050)
		public override void Refresh()
        {
        }

		// 060000cb (1.10.1 RVA 0x19FC060)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { First };
		}

		// 060000cc (1.10.1 RVA 0x19FC140)
		public DebugLogNode()
		{
		}
	}
}
