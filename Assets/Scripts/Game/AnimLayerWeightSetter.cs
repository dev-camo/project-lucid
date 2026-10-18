using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020000a6; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class AnimLayerWeightSetter : MonoBehaviour
	{
		[SerializeField]
		private Animator m_animator;

		[SerializeField]
		private string m_layerName;

		[SerializeField]
		private float m_layerWeight = 1f;

		// 0600054f (1.10.1 RVA 0x52FB60)
		public void Start()
		{
			if (string.IsNullOrEmpty(m_layerName) || !m_animator)
				return;
			m_animator.SetLayerWeight(m_animator.GetLayerIndex(m_layerName), m_layerWeight);
		}

		// 06000550 (1.10.1 RVA 0x52FBF0)
		public AnimLayerWeightSetter()
		{
		}
	}
}
