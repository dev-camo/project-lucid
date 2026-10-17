using System.Collections.Generic;
using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200006d; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	public abstract class NumberConditional<T> : BinderNode
	{
		[NodeInput]
		[RestrictType(typeof(int))]
		public BindReference InputOne = new BindReference();

		[NodeInput]
		[RestrictType(typeof(int))]
		public BindReference InputTwo = new BindReference();

		[NodeInput]
		[RestrictType(typeof(object))]
		public BindReference IfEqual = new BindReference();

		[NodeInput]
		[RestrictType(typeof(object))]
		public BindReference IfLessThan = new BindReference();

		[NodeInput]
		[RestrictType(typeof(object))]
		public BindReference IfGreaterThan = new BindReference();

		[NodeOutput]
		public readonly Bindable<T> Output = new Bindable<T>();

		// 0600014c (1.10.1 RVA 0x185E880)
		public override void Refresh()
		{
			if (InputOne.GetValue<int>() > InputTwo.GetValue<int>())
			{
				Output.Value = IfGreaterThan.GetValue<T>();
			}
			else if (InputOne.GetValue<int>() < InputTwo.GetValue<int>())
			{
				Output.Value = IfLessThan.GetValue<T>();
			}
			else
			{
				Output.Value = IfEqual.GetValue<T>();
			}
		}

		// 0600014d (1.10.1 RVA 0x185E9C0)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { InputOne, InputTwo, IfEqual, IfLessThan, IfGreaterThan };
		}

		// 0600014e (1.10.1 RVA 0x185EC00)
		protected NumberConditional()
		{
		}
	}
}
