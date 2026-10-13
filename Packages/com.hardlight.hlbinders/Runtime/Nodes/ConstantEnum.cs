using System;
using UnityEngine;

namespace Hardlight.UI.Binding
{
	// Original HLBinders.Runtime 0200001b; bodies translated from the 1.10.1 x86-64 binary.
	[AddNodeMenu("Constants/Enum", int.MaxValue)]
	public class ConstantEnum : BinderNode
	{
		[NodeOutput]
		public readonly Bindable<Enum> Output = new Bindable<Enum>();

		[SerializeField]
		private int enumValue;

		[SerializeField]
		private string assemblyQualifiedTypeName;

		private Type enumType;

		// 06000089 (1.10.1 RVA 0x19F9F30)
		public override void Refresh()
		{
			Output.Value = enumType == null ? null : (Enum)Enum.ToObject(enumType, enumValue);
		}

		// 0600008a (1.10.1 RVA 0x19FA040)
		protected override void Awake()
		{
			SetUpBindReferenceFields();
			hideFlags = ShowInNodeEditor ? HideFlags.HideInInspector : HideFlags.None;
			enumType = string.IsNullOrEmpty(assemblyQualifiedTypeName) ? null : Type.GetType(assemblyQualifiedTypeName);
		}

		// 0600008b (1.10.1 RVA 0x19FA120)
		protected override void SetUpBindReferenceFields()
		{
		}

		// 0600008c (1.10.1 RVA 0x19FA130)
		public ConstantEnum()
		{
		}
	}
}
