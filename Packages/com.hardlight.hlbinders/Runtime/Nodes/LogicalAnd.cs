using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200004b; bodies translated from the 1.10.1 x86-64 binary.
	[AddNodeMenu("Logic/And", int.MaxValue)]
	[AddComponentMenu("")]
	public class LogicalAnd : BinderNode
	{
		[RestrictType(typeof(bool))]
		[NodeInput]
		public BindReference First = new BindReference();

		[RestrictType(typeof(bool))]
		[NodeInput]
		public BindReference Second = new BindReference();

		[NodeOutput]
		public readonly Bindable<bool> Output = new Bindable<bool>();

		// 060000eb (1.10.1 RVA 0x19FE110)
		public override void Refresh()
		{
			Output.Value = First.GetValue<bool>() && Second.GetValue<bool>();
		}

		// 060000ec (1.10.1 RVA 0x19FE1A0)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { First, Second };
		}

		// 060000ed (1.10.1 RVA 0x19FE2E0)
		public LogicalAnd()
		{
		}
	}
}
