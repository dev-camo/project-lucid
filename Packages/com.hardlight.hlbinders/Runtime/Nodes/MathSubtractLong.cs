using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200006b; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Math/Subtract Long", int.MaxValue)]
	public class MathSubtractLong : BinderNode
	{
		[RestrictType(typeof(long))]
		[NodeInput]
		public BindReference First = new BindReference();

		[NodeInput]
		[RestrictType(typeof(long))]
		public BindReference Second = new BindReference();

		[NodeOutput]
		public readonly Bindable<long> Output = new Bindable<long>();

		// 06000146 (1.10.1 RVA 0x1A05F70)
		public override void Refresh()
		{
			Output.Value = First.GetValue<long>() - Second.GetValue<long>();
		}

		// 06000147 (1.10.1 RVA 0x1A06000)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { First, Second };
		}

		// 06000148 (1.10.1 RVA 0x1A06140)
		public MathSubtractLong()
		{
		}
	}
}
