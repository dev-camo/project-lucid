using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020002a4; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class AnimatorSetFloatCurve : StateMachineBehaviour
	{
		[SerializeField]
		private string m_floatParameterToSet;

		[Tooltip("This uses the normalised animation clip time, so x=1 is the end of the first loop of the animation.")]
		[SerializeField]
		private AnimationCurve m_valueOverTime;

		[SerializeField]
		[Tooltip("On state exit, determines if animator parameter is set to the end time value.")]
		private bool m_snapToEndOnExit;

		private bool m_valid;

		private int m_parameterHash;

		// 06000e89 (1.10.1 RVA 0x727410)
		public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
		{
			m_valid = !string.IsNullOrEmpty(m_floatParameterToSet);
			if (!m_valid)
				return;
			m_parameterHash = Animator.StringToHash(m_floatParameterToSet);
			UpdateValue(animator, stateInfo);
		}

		// 06000e8a (1.10.1 RVA 0x7274F0)
		public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
		{
			if (m_valid)
				UpdateValue(animator, stateInfo);
		}

		// 06000e8b (1.10.1 RVA 0x7274A0): the curve is evaluated at the unwrapped
		// normalised time.
		private void UpdateValue(Animator animator, AnimatorStateInfo stateInfo)
		{
			animator.SetFloat(m_parameterHash, m_valueOverTime.Evaluate(stateInfo.normalizedTime));
		}

		// 06000e8c (1.10.1 RVA 0x727560): "the end" is the time of the curve's last key.
		public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
		{
			if (m_valid && m_snapToEndOnExit)
				animator.SetFloat(m_parameterHash, m_valueOverTime.Evaluate(m_valueOverTime.TotalTime()));
		}

		// 06000e8d (1.10.1 RVA 0x7275C0)
		public AnimatorSetFloatCurve()
		{
		}
	}
}
