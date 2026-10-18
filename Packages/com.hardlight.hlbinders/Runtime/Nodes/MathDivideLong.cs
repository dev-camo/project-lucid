using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000062; bodies translated from the 1.10.1 x86-64 binary.
	[AddNodeMenu("Math/Divide Long", int.MaxValue)]
	[AddComponentMenu("")]
	public class MathDivideLong : BinderNode
	{
		[NodeInput]
		[RestrictType(typeof(long))]
		public BindReference Numerator = new BindReference();

		[RestrictType(typeof(long))]
		[NodeInput]
		public BindReference Denominator = new BindReference();

		[NodeOutput]
		public readonly Bindable<float> Output = new Bindable<float>();

		// 0600012b (1.10.1 RVA 0x1A03E30)
		public override void Refresh()
		{
			// The denominator is read first; a zero denominator yields 0 without reading the numerator.
			long denominator = Denominator.GetValue<long>();
			Output.Value = denominator != 0 ? (float)Numerator.GetValue<long>() / denominator : 0f;
		}

		// 0600012c (1.10.1 RVA 0x1A03EE0)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { Numerator, Denominator };
		}

		// 0600012d (1.10.1 RVA 0x1A04020)
		public MathDivideLong()
		{
		}
	}
}
