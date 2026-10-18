using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000041; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Logic/EnumEqualsInt", int.MaxValue)]
	public class EnumIntEquality : BinderNode
	{
		[NodeInput]
		[RestrictType(typeof(Enum))]
		public BindReference First = new BindReference();

		[RestrictType(typeof(int))]
		[NodeInput]
		public BindReference Second = new BindReference();

		[NodeOutput]
		public readonly Bindable<bool> Output = new Bindable<bool>();

		// 060000cd (1.10.1 RVA 0x19FC260)
		public override void Refresh()
		{
			int first = First.GetValue<int>();
			int second = Second.GetValue<int>();
			Output.Value = first == second;
		}

		// 060000ce (1.10.1 RVA 0x19FC2F0)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { First, Second };
		}

		// 060000cf (1.10.1 RVA 0x19FC430)
		public EnumIntEquality()
		{
		}
	}
}
