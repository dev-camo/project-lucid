using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000053; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Logic/Not", int.MaxValue)]
	public class LogicalNot : BinderNode
	{
		[NodeInput]
		[RestrictType(typeof(bool))]
		public BindReference Input = new BindReference();

		[NodeOutput]
		public readonly Bindable<bool> Output = new Bindable<bool>();

		// 06000103 (1.10.1 RVA 0x1A00D50)
		public override void Refresh()
		{
			Output.Value = !Input.GetValue<bool>();
		}

		// 06000104 (1.10.1 RVA 0x1A00DC0)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { Input };
		}

		// 06000105 (1.10.1 RVA 0x1A00EA0)
		public LogicalNot()
		{
		}
	}
}
