using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000052; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Logic/Less Than", int.MaxValue)]
	public class LogicalLessThan : BinderNode
	{
		[NodeInput]
		public BindReference First = new BindReference();

		[NodeInput]
		public BindReference Second = new BindReference();

		[NodeOutput]
		public readonly Bindable<bool> Output = new Bindable<bool>();

		// 06000100 (1.10.1 RVA 0x1A008F0)
		public override void Refresh()
		{
			IComparable first = First.GetValue<object>() as IComparable;
			Output.Value = first != null && first.CompareTo(Second.GetValue<object>()) < 0;
		}

		// 06000101 (1.10.1 RVA 0x1A00A30)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { First, Second };
		}

		// 06000102 (1.10.1 RVA 0x1A00B70)
		public LogicalLessThan()
		{
		}
	}
}
