using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000044; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Logic/Equals", int.MaxValue)]
	public class Equality : BinderNode
	{
		[NodeInput]
		public BindReference First = new BindReference();

		[NodeInput]
		public BindReference Second = new BindReference();

		[NodeOutput]
		public readonly Bindable<bool> Output = new Bindable<bool>();

		// 060000d6 (1.10.1 RVA 0x19FCC00)
		public override void Refresh()
		{
			object first = First.GetValue<object>();
			object second = Second.GetValue<object>();
			// Instance Equals on the first value; a null first value matches only a null second.
			Output.Value = first != null ? first.Equals(second) : second == null;
		}

		// 060000d7 (1.10.1 RVA 0x19FCCB0)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { First, Second };
		}

		// 060000d8 (1.10.1 RVA 0x19FCDF0)
		public Equality()
		{
		}
	}
}
