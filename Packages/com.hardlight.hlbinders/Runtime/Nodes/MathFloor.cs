using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000063; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Math/FloorToInt", int.MaxValue)]
	public class MathFloor : BinderNode
	{
		[RestrictType(typeof(float))]
		[NodeInput]
		public BindReference First = new BindReference();

		[NodeOutput]
		public readonly Bindable<int> Output = new Bindable<int>();

		// 0600012e (1.10.1 RVA 0x1A04200)
		public override void Refresh()
		{
			Output.Value = Mathf.FloorToInt(First.GetValue<float>());
		}

		// 0600012f (1.10.1 RVA 0x1A042E0)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { First };
		}

		// 06000130 (1.10.1 RVA 0x1A043C0)
		public MathFloor()
		{
		}
	}
}
