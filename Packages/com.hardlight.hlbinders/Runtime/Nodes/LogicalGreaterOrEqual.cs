using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200004f; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Logic/GTE (at least)", int.MaxValue)]
	public class LogicalGreaterOrEqual : BinderNode
	{
		[NodeInput]
		public BindReference First = new BindReference();

		[NodeInput]
		public BindReference Second = new BindReference();

		[NodeOutput]
		public readonly Bindable<bool> Output = new Bindable<bool>();

		// 060000f7 (1.10.1 RVA 0x19FF960)
		public override void Refresh()
		{
			IComparable first = First.GetValue<object>() as IComparable;
			Output.Value = first != null && first.CompareTo(Second.GetValue<object>()) >= 0;
		}

		// 060000f8 (1.10.1 RVA 0x19FFAA0)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { First, Second };
		}

		// 060000f9 (1.10.1 RVA 0x19FFBE0)
		public LogicalGreaterOrEqual()
		{
		}
	}
}
