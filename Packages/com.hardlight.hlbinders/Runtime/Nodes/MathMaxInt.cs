using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000065; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Math/Max (Int)", int.MaxValue)]
	public class MathMaxInt : BinderNode
	{
		[NodeInput]
		[RestrictType(typeof(int))]
		public BindReference First = new BindReference();

		[NodeInput]
		[RestrictType(typeof(int))]
		public BindReference Second = new BindReference();

		[NodeOutput]
		public readonly Bindable<int> Output = new Bindable<int>();

		// 06000134 (1.10.1 RVA 0x1A048F0)
		public override void Refresh()
		{
			Output.Value = Math.Max(First.GetValue<int>(), Second.GetValue<int>());
		}

		// 06000135 (1.10.1 RVA 0x1A049B0)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { First, Second };
		}

		// 06000136 (1.10.1 RVA 0x1A04AF0)
		public MathMaxInt()
		{
		}
	}
}
