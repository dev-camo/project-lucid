using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200004e; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Logic/AnyOf3", int.MaxValue)]
	public class LogicalAnyOf3 : BinderNode
	{
		[NodeInput]
		[RestrictType(typeof(bool))]
		public BindReference First = new BindReference();

		[NodeInput]
		[RestrictType(typeof(bool))]
		public BindReference Second = new BindReference();

		[NodeInput]
		[RestrictType(typeof(bool))]
		public BindReference Third = new BindReference();

		[NodeOutput]
		public readonly Bindable<bool> Output = new Bindable<bool>();

		// 060000f4 (1.10.1 RVA 0x19FF4D0)
		public override void Refresh()
		{
			Output.Value = First.GetValue<bool>() || Second.GetValue<bool>() || Third.GetValue<bool>();
		}

		// 060000f5 (1.10.1 RVA 0x19FF580)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { First, Second, Third };
		}

		// 060000f6 (1.10.1 RVA 0x19FF710)
		public LogicalAnyOf3()
		{
		}
	}
}
