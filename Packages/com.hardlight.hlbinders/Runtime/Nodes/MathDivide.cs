using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000061; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Math/Divide", int.MaxValue)]
	public class MathDivide : BinderNode
	{
		[RestrictType(typeof(int))]
		[NodeInput]
		public BindReference Numerator = new BindReference();

		[RestrictType(typeof(int))]
		[NodeInput]
		public BindReference Denominator = new BindReference();

		[NodeOutput]
		public readonly Bindable<float> Output = new Bindable<float>();

		// 06000128 (1.10.1 RVA 0x1A03A70)
		public override void Refresh()
		{
			// Float division with no zero guard.
			Output.Value = (float)Numerator.GetValue<int>() / Denominator.GetValue<int>();
		}

		// 06000129 (1.10.1 RVA 0x1A03B10)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { Numerator, Denominator };
		}

		// 0600012a (1.10.1 RVA 0x1A03C50)
		public MathDivide()
		{
		}
	}
}
