using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200005b; bodies translated from the 1.10.1 x86-64 binary.
	// Only the first input equal to Input raises its toggle; bindReferences[i] pairs with bindOutputs[i].
	[AddComponentMenu("")]
	[AddNodeMenu("Logic/Switch-Toggle", int.MaxValue)]
	public class LogicalToggle : BinderNode
	{
		[NodeInput]
		public BindReference Input = new BindReference();

		[NodeInput]
		public BindReference First = new BindReference();

		[NodeInput]
		public BindReference Second = new BindReference();

		[NodeInput]
		public BindReference Third = new BindReference();

		[NodeInput]
		public BindReference Fourth = new BindReference();

		[NodeInput]
		public BindReference Fifth = new BindReference();

		[NodeInput]
		public BindReference Sixth = new BindReference();

		[NodeOutput]
		public readonly Bindable<bool> AnyMatch = new Bindable<bool>();

		[NodeOutput]
		public readonly Bindable<bool> MatchFirst = new Bindable<bool>();

		[NodeOutput]
		public readonly Bindable<bool> MatchSecond = new Bindable<bool>();

		[NodeOutput]
		public readonly Bindable<bool> MatchThird = new Bindable<bool>();

		[NodeOutput]
		public readonly Bindable<bool> MatchFourth = new Bindable<bool>();

		[NodeOutput]
		public readonly Bindable<bool> MatchFifth = new Bindable<bool>();

		[NodeOutput]
		public readonly Bindable<bool> MatchSixth = new Bindable<bool>();

		private List<Bindable<bool>> bindOutputs;

		// 06000112 (1.10.1 RVA 0x1A01500)
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

		// 06000113 (1.10.1 RVA 0x1A019E0)
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

		// 06000114 (1.10.1 RVA 0x1A01A80)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { Input, First, Second, Third, Fourth, Fifth, Sixth };
		}

		// 06000115 (1.10.1 RVA 0x1A016D0)
		private void SetupBinderOutputs()
		{
			if (bindOutputs != null) return;
			bindOutputs = new List<Bindable<bool>> { AnyMatch, MatchFirst, MatchSecond, MatchThird, MatchFourth, MatchFifth, MatchSixth };
		}

		// 06000116 (1.10.1 RVA 0x1A01D60)
		public LogicalToggle()
		{
		}
	}
}
