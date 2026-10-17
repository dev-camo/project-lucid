using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding
{
	// Original HLBinders.Runtime 0200000c; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Input", 0)]
	public class BinderInput : BinderNode
	{
		[NodeOutput]
		public BindReference Bindable = new BindReference();

		// 06000010 (1.10.1 RVA 0x19F37F0)
		protected override void OnEnable()
        {
            hideFlags = ShowInNodeEditor ? HideFlags.HideInInspector : HideFlags.None;
        }

		// 06000011 (1.10.1 RVA 0x19F3840)
		protected override void OnDisable()
{ }

		// 06000012 (1.10.1 RVA 0x19F3850)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new List<BindReference> { Bindable };
		}

		// 06000013 (1.10.1 RVA 0x19F3930)
		public BinderInput()
		{
		}
	}
}
