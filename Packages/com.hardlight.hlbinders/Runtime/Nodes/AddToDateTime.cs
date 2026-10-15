using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200001c; bodies translated from the 1.10.1 x86-64 binary.
	[AddNodeMenu("Time/AddToDateTime", int.MaxValue)]
	[AddComponentMenu("")]
	public class AddToDateTime : BinderNode
	{
		[NodeInput]
		[RestrictType(typeof(TimeSpan))]
		public BindReference TimeSpanToAdd = new BindReference();

		[RestrictType(typeof(DateTime))]
		[NodeInput]
		public BindReference DateTimeToAddTo = new BindReference();

		[NodeOutput]
		public Bindable<DateTime> Output = new Bindable<DateTime>();

		// 0600008d (1.10.1 RVA 0x19FA210)
		public override void Refresh()
		{
			TimeSpan span = TimeSpanToAdd.GetValue<TimeSpan>();
			Output.Value = DateTimeToAddTo.GetValue<DateTime>().Add(span);
		}

		// 0600008e (1.10.1 RVA 0x19FA2E0)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new System.Collections.Generic.List<BindReference> { TimeSpanToAdd, DateTimeToAddTo };
		}

		// 0600008f (1.10.1 RVA 0x19FA420)
		public AddToDateTime()
		{
		}
	}
}
