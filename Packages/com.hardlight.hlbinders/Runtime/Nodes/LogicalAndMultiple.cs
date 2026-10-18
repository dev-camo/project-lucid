using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200004c; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Logic/All Of", int.MaxValue)]
	public class LogicalAndMultiple : BinderNode
	{
		[NodeInput]
		[RestrictType(typeof(bool))]
		public BindReference First = new BindReference();

		[RestrictType(typeof(bool))]
		[NodeInput]
		public BindReference Second = new BindReference();

		[NodeInput]
		[RestrictType(typeof(bool))]
		public BindReference Third = new BindReference();

		[RestrictType(typeof(bool))]
		[NodeInput]
		public BindReference Fourth = new BindReference();

		[NodeInput]
		[RestrictType(typeof(bool))]
		public BindReference Fifth = new BindReference();

		[NodeInput]
		[RestrictType(typeof(bool))]
		public BindReference Sixth = new BindReference();

		[NodeOutput]
		public readonly Bindable<bool> Output = new Bindable<bool>();

		// 060000ee (1.10.1 RVA 0x19FE4C0)
		public override void Refresh()
		{
			// Unset inputs are skipped; once false, later set inputs are not read.
			bool result = true;
			if (First.IsSet)
			{
				result = First.GetValue<bool>();
			}
			if (Second.IsSet)
			{
				result = result && Second.GetValue<bool>();
			}
			if (Third.IsSet)
			{
				result = result && Third.GetValue<bool>();
			}
			if (Fourth.IsSet)
			{
				result = result && Fourth.GetValue<bool>();
			}
			if (Fifth.IsSet)
			{
				result = result && Fifth.GetValue<bool>();
			}
			if (Sixth.IsSet)
			{
				result = result && Sixth.GetValue<bool>();
			}
			Output.Value = result;
		}

		// 060000ef (1.10.1 RVA 0x19FE6E0)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { First, Second, Third, Fourth, Fifth, Sixth };
		}

		// 060000f0 (1.10.1 RVA 0x19FE970)
		public LogicalAndMultiple()
		{
		}
	}
}
