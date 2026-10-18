using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 02000073; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Time/Progress", int.MaxValue)]
	public class Progress : BinderNode
	{
		[NodeInput]
		[RestrictType(typeof(DateTime))]
		public BindReference StartTime = new BindReference();

		[RestrictType(typeof(DateTime))]
		[NodeInput]
		public BindReference EndTime = new BindReference();

		[NodeInput]
		[RestrictType(typeof(DateTime))]
		public BindReference CurrentTime = new BindReference();

		[NodeOutput]
		public readonly Bindable<TimeSpan> ElapsedTime = new Bindable<TimeSpan>();

		[NodeOutput]
		public readonly Bindable<TimeSpan> RemainingTime = new Bindable<TimeSpan>();

		[NodeOutput]
		public readonly Bindable<float> ProgressAmount = new Bindable<float>();

		// 0600015c (1.10.1 RVA 0x1A07610)
		public override void Refresh()
		{
			DateTime current = CurrentTime.GetValue<DateTime>();
			DateTime end = EndTime.GetValue<DateTime>();
			// An end equal to default(DateTime), including an unset one, collapses onto the current time; the current time is clamped only at the end.
			if (end == default(DateTime))
			{
				end = current;
			}
			if (current > end)
			{
				current = end;
			}
			RemainingTime.Value = end - current;
			if (StartTime.IsSet)
			{
				DateTime start = StartTime.GetValue<DateTime>();
				TimeSpan elapsed = current - start;
				TimeSpan duration = end - start;
				ElapsedTime.Value = elapsed;
				ProgressAmount.Value = (float)(elapsed.TotalSeconds / duration.TotalSeconds);
			}
			else if (EndTime.IsSet)
			{
				// Without a start time, progress is only finished (1) or not (0); ElapsedTime is left alone.
				ProgressAmount.Value = RemainingTime.Value.TotalSeconds > 0.0 ? 0f : 1f;
			}
		}

		// 0600015d (1.10.1 RVA 0x1A078C0)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { StartTime, EndTime, CurrentTime };
		}

		// 0600015e (1.10.1 RVA 0x1A07A50)
		public Progress()
		{
		}
	}
}
