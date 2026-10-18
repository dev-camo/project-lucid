using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200003b; bodies translated from the 1.10.1 x86-64 binary.
	// Each setter stores its value and then publishes every output through the virtual Refresh.
	[AddComponentMenu("")]
	[AddNodeMenu("Constants/Constant Value (Legacy)", int.MaxValue)]
	public class ConstantValue : BinderNode
	{
		[NodeOutput]
		public readonly Bindable<int> IntOutput = new Bindable<int>();

		[NodeOutput]
		public readonly Bindable<float> FloatOutput = new Bindable<float>();

		[NodeOutput]
		public readonly Bindable<bool> BoolOutput = new Bindable<bool>();

		[NodeOutput]
		public readonly Bindable<string> StringOutput = new Bindable<string>();

		[NodeOutput]
		public readonly Bindable<object> nullOutput = new Bindable<object>();

		[SerializeField]
		private int intValue;

		[SerializeField]
		private float floatValue;

		[SerializeField]
		private bool boolValue;

		[SerializeField]
		private string stringValue;

		// 060000b7/060000b8 (1.10.1 RVA 0x19FB070/0x19FB080)
		public int IntValue
		{
			get { return intValue; }
			set
			{
				intValue = value;
				Refresh();
			}
		}

		// 060000b9/060000ba (1.10.1 RVA 0x19FB0A0/0x19FB0B0)
		public float FloatValue
		{
			get { return floatValue; }
			set
			{
				floatValue = value;
				Refresh();
			}
		}

		// 060000bb/060000bc (1.10.1 RVA 0x19FB0D0/0x19FB0E0)
		public bool BoolValue
		{
			get { return boolValue; }
			set
			{
				boolValue = value;
				Refresh();
			}
		}

		// 060000bd/060000be (1.10.1 RVA 0x19FB100/0x19FB110)
		public string StringValue
		{
			get { return stringValue; }
			set
			{
				stringValue = value;
				Refresh();
			}
		}

		// 060000bf (1.10.1 RVA 0x19FB150)
		public override void Refresh()
		{
			IntOutput.Value = intValue;
			FloatOutput.Value = floatValue;
			BoolOutput.Value = boolValue;
			StringOutput.Value = stringValue;
			nullOutput.Value = null;
		}

		// 060000c0 (1.10.1 RVA 0x19FB210)
		protected override void SetUpBindReferenceFields()
		{
		}

		// 060000c1 (1.10.1 RVA 0x19FB220)
		public ConstantValue()
		{
		}
	}
}
