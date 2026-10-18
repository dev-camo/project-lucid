using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000068; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Math/Multiply (Float)", int.MaxValue)]
	public class MathMultiplyFloat : BinderNode
	{
		[RestrictType(typeof(float))]
		[NodeInput]
		public BindReference First = new BindReference();

		[RestrictType(typeof(float))]
		[NodeInput]
		public BindReference Second = new BindReference();

		[NodeOutput]
		public readonly Bindable<float> Output = new Bindable<float>();

		// 0600013d (1.10.1 RVA 0x1A05440)
		public override void Refresh()
		{
			Output.Value = First.GetValue<float>() * Second.GetValue<float>();
		}

		// 0600013e (1.10.1 RVA 0x1A054E0)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { First, Second };
		}

		// 0600013f (1.10.1 RVA 0x1A05620)
		public MathMultiplyFloat()
		{
		}
	}
}
