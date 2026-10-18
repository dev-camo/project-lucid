using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200005d; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Math/Abs", int.MaxValue)]
	public class MathAbs : BinderNode
	{
		[NodeInput]
		[RestrictType(typeof(int))]
		public BindReference Input = new BindReference();

		[NodeOutput]
		public readonly Bindable<int> Output = new Bindable<int>();

		// 0600011c (1.10.1 RVA 0x1A02C30)
		public override void Refresh()
		{
			// Inlined Mathf.Abs(int): neg/cmovs with no overflow throw for int.MinValue under IL2CPP.
			Output.Value = Mathf.Abs(Input.GetValue<int>());
		}

		// 0600011d (1.10.1 RVA 0x1A02D00)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { Input };
		}

		// 0600011e (1.10.1 RVA 0x1A02DE0)
		public MathAbs()
		{
		}
	}
}
