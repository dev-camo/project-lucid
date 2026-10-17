using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200002f; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	public abstract class Constant<T> : BinderNode
	{
		[NodeOutput]
		public readonly Bindable<T> Output = new Bindable<T>();

		[SerializeField]
		private T value;

		public T Value
		{
			get
=> value;
			set
=> this.value = value;
		}

		// 060000a8 (1.10.1 RVA 0xF0D8E0)
		public override void Refresh()
		{
			Output.Value = value;
		}

		// 060000a9 (1.10.1 RVA 0xF0D910)
		protected override void SetUpBindReferenceFields()
{}

		// 060000aa (1.10.1 RVA 0xF0D920)
		protected Constant()
		{
		}
	}
}
