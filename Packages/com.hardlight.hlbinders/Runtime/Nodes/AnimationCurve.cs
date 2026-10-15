using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
	// Original HLBinders.Runtime 0200001d; bodies translated from the 1.10.1 x86-64 binary.
	[AddComponentMenu("")]
	[AddNodeMenu("Math/Curve", int.MaxValue)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class AnimationCurve : BinderNode
	{
		[NodeInput]
		[RestrictType(typeof(float))]
		public BindReference Time = new BindReference();

		[NodeOutput]
		public readonly Bindable<float> Output = new Bindable<float>();

		[SerializeField]
		private UnityEngine.AnimationCurve m_animationCurve = new UnityEngine.AnimationCurve();

		// 06000090 (1.10.1 RVA 0x19FA600)
		public override void Refresh()
		{
			// A null curve yields 0 without reading Time (0x19FA634).
			Output.Value = m_animationCurve != null ? m_animationCurve.Evaluate(Time.GetValue<float>()) : 0f;
		}

		// 06000091 (1.10.1 RVA 0x19FA680)
		protected override void SetUpBindReferenceFields()
		{
			bindReferences = new System.Collections.Generic.List<BindReference> { Time };
		}

		// 06000092 (1.10.1 RVA 0x19FA760)
		public AnimationCurve()
		{
		}
	}
}
