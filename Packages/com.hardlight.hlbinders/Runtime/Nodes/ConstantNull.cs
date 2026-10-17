using System;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000037; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Constants/Null", int.MaxValue)]
	public class ConstantNull : BinderNode
	{
		[NodeOutput]
		public readonly Bindable<object> Null = new Bindable<object>();

		// 060000b2 (1.10.1 RVA 0x19FAEC0)
		protected override void SetUpBindReferenceFields()
{}

		// 060000b3 (1.10.1 RVA 0x19FAED0)
		public ConstantNull()
		{
		}
	}
}
