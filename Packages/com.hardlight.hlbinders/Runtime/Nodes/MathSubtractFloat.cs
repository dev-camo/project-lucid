using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200006a; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Math/Subtract (Float)", int.MaxValue)]
	public class MathSubtractFloat : BinderNode
	{
		[NodeInput]
		[RestrictType(typeof(float))]
		public BindReference First = new BindReference();

		[NodeInput]
		[RestrictType(typeof(float))]
		public BindReference Second = new BindReference();

		[NodeOutput]
		public readonly Bindable<float> Output = new Bindable<float>();

		// 06000143 (1.10.1 RVA 0x1A05BB0)
		public override void Refresh()
		{
			Output.Value = First.GetValue<float>() - Second.GetValue<float>();
		}

		// 06000144 (1.10.1 RVA 0x1A05C50)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { First, Second };
		}

		// 06000145 (1.10.1 RVA 0x1A05D90)
		public MathSubtractFloat()
		{
		}
	}
}
