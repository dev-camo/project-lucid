using System.Collections.Generic;
using UnityEngine;
using System;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000055; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	public class LogicalSwitch<T> : BinderNode
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
		public readonly Bindable<T> Output = new Bindable<T>();

		[NodeOutput]
		public readonly Bindable<T> AnyMatchOutput = new Bindable<T>();

		[SerializeField]
		private T AnyMatch;

		[SerializeField]
		private T MatchFirst;

		[SerializeField]
		private T MatchSecond;

		[SerializeField]
		private T MatchThird;

		[SerializeField]
		private T MatchFourth;

		[SerializeField]
		private T MatchFifth;

		[SerializeField]
		private T MatchSixth;

		private List<BindReference> switchInputs;

		private List<T> switchOutputs;

		// 06000109 (1.10.1 RVA 0x17B1D70)
		public override void Refresh()
		{
			for (int i = 0; i < switchInputs.Count; i++)
			{
				if (Equality(Input, switchInputs[i]))
				{
					AnyMatchOutput.Value = AnyMatch;
					Output.Value = switchOutputs[i];
					return;
				}
			}
		}

		// 0600010a (1.10.1 RVA 0x17B1F20)
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

		// 0600010b (1.10.1 RVA 0x17B1FC0)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { Input, First, Second, Third, Fourth, Fifth, Sixth };
			switchInputs = new List<BindReference> { First, Second, Third, Fourth, Fifth, Sixth };
			switchOutputs = new List<T> { MatchFirst, MatchSecond, MatchThird, MatchFourth, MatchFifth, MatchSixth };
		}

		// 0600010c (1.10.1 RVA 0x17B27B0)
		public LogicalSwitch()
		{
		}
	}
}
