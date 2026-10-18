using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000049; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Text/Is String Null Or Empty", int.MaxValue)]
	public class IsStringNullOrEmpty : BinderNode
	{
		[NodeInput]
		[RestrictType(typeof(string))]
		public BindReference Input = new BindReference();

		[NodeOutput]
		public readonly Bindable<bool> Output = new Bindable<bool>();

		// 060000e5 (1.10.1 RVA 0x19FDB00)
		public override void Refresh()
		{
			string value = Input.GetValue<string>();
			Output.Value = string.IsNullOrEmpty(value);
		}

		// 060000e6 (1.10.1 RVA 0x19FDB80)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { Input };
		}

		// 060000e7 (1.10.1 RVA 0x19FDC60)
		public IsStringNullOrEmpty()
		{
		}
	}
}
