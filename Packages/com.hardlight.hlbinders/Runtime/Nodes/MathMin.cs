using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000066; bodies translated from the 1.10.1 x86-64 binary.
	[AddNodeMenu("Math/Min", int.MaxValue)]
	[AddComponentMenu("")]
	public class MathMin : BinderNode
	{
		[NodeInput]
		[RestrictType(typeof(float))]
		public BindReference First = new BindReference();

		[NodeInput]
		[RestrictType(typeof(float))]
		public BindReference Second = new BindReference();

		[NodeOutput]
		public readonly Bindable<float> Output = new Bindable<float>();

		// 06000137 (1.10.1 RVA 0x1A04CD0)
		public override void Refresh()
		{
			Output.Value = Mathf.Min(First.GetValue<float>(), Second.GetValue<float>());
		}

		// 06000138 (1.10.1 RVA 0x1A04D70)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { First, Second };
		}

		// 06000139 (1.10.1 RVA 0x1A04EB0)
		public MathMin()
		{
		}
	}
}
