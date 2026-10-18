using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000050; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Logic/Greater Than", int.MaxValue)]
	public class LogicalGreaterThan : BinderNode
	{
		[NodeInput]
		public BindReference First = new BindReference();

		[NodeInput]
		public BindReference Second = new BindReference();

		[NodeOutput]
		public readonly Bindable<bool> Output = new Bindable<bool>();

		// 060000fa (1.10.1 RVA 0x19FFDC0)
		public override void Refresh()
		{
			IComparable first = First.GetValue<object>() as IComparable;
			Output.Value = first != null && first.CompareTo(Second.GetValue<object>()) > 0;
		}

		// 060000fb (1.10.1 RVA 0x19FFF00)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { First, Second };
		}

		// 060000fc (1.10.1 RVA 0x1A00040)
		public LogicalGreaterThan()
		{
		}
	}
}
