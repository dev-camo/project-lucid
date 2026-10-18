using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000051; bodies translated from the 1.10.1 x86-64 binary.
	[AddNodeMenu("Logic/In Set", int.MaxValue)]
	[AddComponentMenu("")]
	public class LogicalInSet : BinderNode
	{
		[NodeInput]
		public BindReference Item = new BindReference();

		[NodeInput]
		public BindReference SetItem1 = new BindReference();

		[NodeInput]
		public BindReference SetItem2 = new BindReference();

		[NodeInput]
		public BindReference SetItem3 = new BindReference();

		[NodeInput]
		public BindReference SetItem4 = new BindReference();

		[NodeOutput]
		public readonly Bindable<bool> Output = new Bindable<bool>();

		// 060000fd (1.10.1 RVA 0x1A00220)
		public override void Refresh()
		{
			object item = Item.GetValue<object>();
			Output.Value = item != null && (item.Equals(SetItem1.GetValue<object>()) || item.Equals(SetItem2.GetValue<object>())
				|| item.Equals(SetItem3.GetValue<object>()) || item.Equals(SetItem4.GetValue<object>()));
		}

		// 060000fe (1.10.1 RVA 0x1A00380)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { Item, SetItem1, SetItem2, SetItem3, SetItem4 };
		}

		// 060000ff (1.10.1 RVA 0x1A005C0)
		public LogicalInSet()
		{
		}
	}
}
