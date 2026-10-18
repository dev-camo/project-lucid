using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000064; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Math/Max", int.MaxValue)]
	public class MathMax : BinderNode
	{
		[NodeInput]
		[RestrictType(typeof(float))]
		public BindReference First = new BindReference();

		[RestrictType(typeof(float))]
		[NodeInput]
		public BindReference Second = new BindReference();

		[NodeOutput]
		public readonly Bindable<float> Output = new Bindable<float>();

		// 06000131 (1.10.1 RVA 0x1A04530)
		public override void Refresh()
		{
			Output.Value = Mathf.Max(First.GetValue<float>(), Second.GetValue<float>());
		}

		// 06000132 (1.10.1 RVA 0x1A045D0)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { First, Second };
		}

		// 06000133 (1.10.1 RVA 0x1A04710)
		public MathMax()
		{
		}
	}
}
