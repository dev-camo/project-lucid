using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200004a; bodies translated from the 1.10.1 x86-64 binary.
	[AddNodeMenu("Math/List Length", int.MaxValue)]
	[AddComponentMenu("")]
	public class ListLength : BinderNode
	{
		[NodeInput]
		[RestrictType(typeof(IList))]
		public BindReference InputList = new BindReference();

		[NodeOutput]
		public readonly Bindable<int> Length = new Bindable<int>();

		// 060000e8 (1.10.1 RVA 0x19FDDD0)
		public override void Refresh()
		{
			// A missing list leaves the previous length in place.
			IList list = InputList.GetValue<IList>();
			if (list != null)
			{
				Length.Value = list.Count;
			}
		}

		// 060000e9 (1.10.1 RVA 0x19FDEC0)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { InputList };
		}

		// 060000ea (1.10.1 RVA 0x19FDFA0)
		public ListLength()
		{
		}
	}
}
