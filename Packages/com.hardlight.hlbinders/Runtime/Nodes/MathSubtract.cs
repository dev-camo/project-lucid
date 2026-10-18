using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000069; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Math/Subtract", int.MaxValue)]
	public class MathSubtract : BinderNode
	{
		[NodeInput]
		[RestrictType(typeof(int))]
		public BindReference First = new BindReference();

		[RestrictType(typeof(int))]
		[NodeInput]
		public BindReference Second = new BindReference();

		[NodeOutput]
		public readonly Bindable<int> Output = new Bindable<int>();

		// 06000140 (1.10.1 RVA 0x1A05800)
		public override void Refresh()
		{
			Output.Value = First.GetValue<int>() - Second.GetValue<int>();
		}

		// 06000141 (1.10.1 RVA 0x1A05890)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { First, Second };
		}

		// 06000142 (1.10.1 RVA 0x1A059D0)
		public MathSubtract()
		{
		}
	}
}
