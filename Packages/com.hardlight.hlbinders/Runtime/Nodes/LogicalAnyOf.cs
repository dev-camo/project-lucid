using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200004d; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Logic/AnyOf", int.MaxValue)]
	public class LogicalAnyOf : BinderNode
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

		[RestrictType(typeof(bool))]
		[NodeInput]
		public BindReference Fourth = new BindReference();

		[RestrictType(typeof(bool))]
		[NodeInput]
		public BindReference Fifth = new BindReference();

		[NodeInput]
		[RestrictType(typeof(bool))]
		public BindReference Sixth = new BindReference();

		public BindReference Seventh = new BindReference();

		[NodeOutput]
		public readonly Bindable<bool> Output = new Bindable<bool>();

		// 060000f1 (1.10.1 RVA 0x19FED20)
		public override void Refresh()
		{
			// Seventh (no NodeInput attribute) is constructed but neither read nor listened to.
			Output.Value = First.GetValue<bool>() || Second.GetValue<bool>() || Third.GetValue<bool>()
				|| Fourth.GetValue<bool>() || Fifth.GetValue<bool>() || Sixth.GetValue<bool>();
		}

		// 060000f2 (1.10.1 RVA 0x19FEE20)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { First, Second, Third, Fourth, Fifth, Sixth };
		}

		// 060000f3 (1.10.1 RVA 0x19FF0B0)
		public LogicalAnyOf()
		{
		}
	}
}
