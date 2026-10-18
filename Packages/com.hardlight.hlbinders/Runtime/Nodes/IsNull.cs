using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000048; bodies translated from the 1.10.1 x86-64 binary.
	[AddNodeMenu("Logic/Is Null", int.MaxValue)]
	[AddComponentMenu("")]
	public class IsNull : BinderNode
	{
		[NodeInput]
		public BindReference Input = new BindReference();

		[NodeOutput]
		public readonly Bindable<bool> Output = new Bindable<bool>();

		// 060000e2 (1.10.1 RVA 0x19FD840)
		public override void Refresh()
		{
			object value = Input.GetValue<object>();
			Output.Value = value == null;
		}

		// 060000e3 (1.10.1 RVA 0x19FD8B0)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { Input };
		}

		// 060000e4 (1.10.1 RVA 0x19FD990)
		public IsNull()
		{
		}
	}
}
