using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000060; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Math/Add Long", int.MaxValue)]
	public class MathAddLong : BinderNode
	{
		[RestrictType(typeof(long))]
		[NodeInput]
		public BindReference First = new BindReference();

		[RestrictType(typeof(long))]
		[NodeInput]
		public BindReference Second = new BindReference();

		[NodeOutput]
		public readonly Bindable<long> Output = new Bindable<long>();

		// 06000125 (1.10.1 RVA 0x1A036C0)
		public override void Refresh()
		{
			Output.Value = First.GetValue<long>() + Second.GetValue<long>();
		}

		// 06000126 (1.10.1 RVA 0x1A03750)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { First, Second };
		}

		// 06000127 (1.10.1 RVA 0x1A03890)
		public MathAddLong()
		{
		}
	}
}
