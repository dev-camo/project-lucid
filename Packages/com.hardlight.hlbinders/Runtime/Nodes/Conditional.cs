using System.Collections.Generic;
using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200001e; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	public abstract class Conditional<T> : BinderNode
	{
		[RestrictType(typeof(bool))]
		[NodeInput]
		public BindReference Input = new BindReference();

		[RestrictType(typeof(object))]
		[NodeInput]
		public BindReference IfTrue = new BindReference();

		[NodeInput]
		[RestrictType(typeof(object))]
		public BindReference IfFalse = new BindReference();

		[NodeOutput]
		public readonly Bindable<T> Output = new Bindable<T>();

		// 06000093 (1.10.1 RVA 0xF06730)
		public override void Refresh()
		{
			Output.Value = Input.GetValue<bool>() ? IfTrue.GetValue<T>() : IfFalse.GetValue<T>();
		}

		// 06000094 (1.10.1 RVA 0xF067D0)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { Input, IfTrue, IfFalse };
		}

		// 06000095 (1.10.1 RVA 0xF06960)
		protected Conditional()
		{
		}
	}
}
