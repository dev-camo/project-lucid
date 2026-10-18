using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200005c; bodies translated from the 1.10.1 x86-64 binary.
	// Three-input form of LogicalToggle: only the first input equal to Input raises its toggle.
	[AddComponentMenu("")]
	[AddNodeMenu("Logic/Switch-Toggle3", int.MaxValue)]
	public class LogicalToggle3 : BinderNode
	{
		[NodeInput]
		public BindReference Input = new BindReference();

		[NodeInput]
		public BindReference First = new BindReference();

		[NodeInput]
		public BindReference Second = new BindReference();

		[NodeInput]
		public BindReference Third = new BindReference();

		[NodeOutput]
		public readonly Bindable<bool> AnyMatch = new Bindable<bool>();

		[NodeOutput]
		public readonly Bindable<bool> MatchFirst = new Bindable<bool>();

		[NodeOutput]
		public readonly Bindable<bool> MatchSecond = new Bindable<bool>();

		[NodeOutput]
		public readonly Bindable<bool> MatchThird = new Bindable<bool>();

		private List<Bindable<bool>> bindOutputs;

		// 06000117 (1.10.1 RVA 0x1A02290)
		public override void Refresh()
		{
			SetupBinderOutputs();
			int match = -1;
			for (int i = 1; i < bindReferences.Count; i++)
			{
				if (Equality(Input, bindReferences[i]))
				{
					match = i;
					break;
				}
			}
			AnyMatch.Value = match >= 0;
			for (int i = 1; i < bindOutputs.Count; i++)
				bindOutputs[i].Value = match == i;
		}

		// 06000118 (1.10.1 RVA 0x1A02660)
		private bool Equality(BindReference a, BindReference b)
		{
			object valueA = a.GetValue<object>();
			object valueB = b.GetValue<object>();
			if (valueA == null)
			{
				return valueB == null;
			}
			return valueA.Equals(valueB);
		}

		// 06000119 (1.10.1 RVA 0x1A02700)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { Input, First, Second, Third };
		}

		// 0600011a (1.10.1 RVA 0x1A02460)
		private void SetupBinderOutputs()
		{
			if (bindOutputs != null) return;
			bindOutputs = new List<Bindable<bool>> { AnyMatch, MatchFirst, MatchSecond, MatchThird };
		}

		// 0600011b (1.10.1 RVA 0x1A028E0)
		public LogicalToggle3()
		{
		}
	}
}
