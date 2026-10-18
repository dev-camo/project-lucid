using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200005e; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Math/Add", int.MaxValue)]
	public class MathAdd : BinderNode
	{
		[NodeInput]
		[RestrictType(typeof(int))]
		public BindReference First = new BindReference();

		[NodeInput]
		[RestrictType(typeof(int))]
		public BindReference Second = new BindReference();

		[NodeOutput]
		public readonly Bindable<int> Output = new Bindable<int>();

		// 0600011f (1.10.1 RVA 0x1A02F50)
		public override void Refresh()
		{
			Output.Value = First.GetValue<int>() + Second.GetValue<int>();
		}

		// 06000120 (1.10.1 RVA 0x1A02FE0)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { First, Second };
		}

		// 06000121 (1.10.1 RVA 0x1A03120)
		public MathAdd()
		{
		}
	}
}
