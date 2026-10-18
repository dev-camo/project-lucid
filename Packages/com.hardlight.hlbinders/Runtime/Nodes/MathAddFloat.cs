using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200005f; bodies translated from the 1.10.1 x86-64 binary.
	[AddNodeMenu("Math/Add (Float)", int.MaxValue)]
	[AddComponentMenu("")]
	public class MathAddFloat : BinderNode
	{
		[NodeInput]
		[RestrictType(typeof(float))]
		public BindReference First = new BindReference();

		[NodeInput]
		[RestrictType(typeof(float))]
		public BindReference Second = new BindReference();

		[NodeOutput]
		public readonly Bindable<float> Output = new Bindable<float>();

		// 06000122 (1.10.1 RVA 0x1A03300)
		public override void Refresh()
		{
			Output.Value = First.GetValue<float>() + Second.GetValue<float>();
		}

		// 06000123 (1.10.1 RVA 0x1A033A0)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { First, Second };
		}

		// 06000124 (1.10.1 RVA 0x1A034E0)
		public MathAddFloat()
		{
		}
	}
}
