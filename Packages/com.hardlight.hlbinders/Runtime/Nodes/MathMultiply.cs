using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000067; bodies translated from the 1.10.1 x86-64 binary.
	[AddNodeMenu("Math/Multiply", int.MaxValue)]
	[AddComponentMenu("")]
	public class MathMultiply : BinderNode
	{
		[RestrictType(typeof(int))]
		[NodeInput]
		public BindReference First = new BindReference();

		[RestrictType(typeof(int))]
		[NodeInput]
		public BindReference Second = new BindReference();

		[NodeOutput]
		public readonly Bindable<int> Output = new Bindable<int>();

		// 0600013a (1.10.1 RVA 0x1A05090)
		public override void Refresh()
		{
			Output.Value = First.GetValue<int>() * Second.GetValue<int>();
		}

		// 0600013b (1.10.1 RVA 0x1A05120)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { First, Second };
		}

		// 0600013c (1.10.1 RVA 0x1A05260)
		public MathMultiply()
		{
		}
	}
}
