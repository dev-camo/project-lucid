using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000054; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Logic/Or", int.MaxValue)]
	public class LogicalOr : BinderNode
	{
		[NodeInput]
		[RestrictType(typeof(bool))]
		public BindReference First = new BindReference();

		[NodeInput]
		[RestrictType(typeof(bool))]
		public BindReference Second = new BindReference();

		[NodeOutput]
		public readonly Bindable<bool> Output = new Bindable<bool>();

		// 06000106 (1.10.1 RVA 0x1A01010)
		public override void Refresh()
		{
			Output.Value = First.GetValue<bool>() || Second.GetValue<bool>();
		}

		// 06000107 (1.10.1 RVA 0x1A010A0)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { First, Second };
		}

		// 06000108 (1.10.1 RVA 0x1A011E0)
		public LogicalOr()
		{
		}
	}
}
