using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020000a3; bodies translated from the 1.10.1 x86-64 binary.
	[CreateAssetMenu(fileName = "AnimatorToMaterialPropertyData", menuName = "HardlightProject/AnimatorToMaterialPropertyData")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class AnimatorToMaterialPropertyData : ScriptableObject
	{
		// Original Game.Runtime 020000a4.
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Serializable]
		public class ParameterToPropertyPair
		{
			[SerializeField]
			private string m_animParameter;

			[SerializeField]
			private string m_materialProperty;

			[NonSerialized]
			private int m_animParameterHash;

			[NonSerialized]
			private int m_materialPropertyHash;

			// 06000543 (1.10.1 RVA 0x52F8F0)
			public string AnimParameter => m_animParameter;

			// 06000544 (1.10.1 RVA 0x52F900)
			public int AnimParameterHash => m_animParameterHash;

			// 06000545 (1.10.1 RVA 0x52F910)
			public string MaterialProperty => m_materialProperty;

			// 06000546 (1.10.1 RVA 0x52F920)
			public int MaterialPropertyHash => m_materialPropertyHash;

			// 06000547 (1.10.1 RVA 0x52F8B0)
			public void UpdateCachedValues()
			{
				m_animParameterHash = Animator.StringToHash(m_animParameter);
				m_materialPropertyHash = Shader.PropertyToID(m_materialProperty);
			}

			// 06000548 (1.10.1 RVA 0x52F930)
			public ParameterToPropertyPair()
			{
			}
		}

		[SerializeField]
		private ParameterToPropertyPair[] m_parametersToProperties;

		// 0600053e (1.10.1 RVA 0x52F780)
		public ParameterToPropertyPair[] ParametersToProperties => m_parametersToProperties;

		// 0600053f (1.10.1 RVA 0x52F790)
		private void OnValidate()
		{
			UpdateCachedValues();
		}

		// 06000540 (1.10.1 RVA 0x52F850)
		private void Awake()
		{
			UpdateCachedValues();
		}

		// 06000541 (1.10.1 RVA 0x52F7F0)
		public void UpdateCachedValues()
		{
			foreach (ParameterToPropertyPair pair in m_parametersToProperties)
				pair.UpdateCachedValues();
		}

		// 06000542 (1.10.1 RVA 0x52F8E0)
		public AnimatorToMaterialPropertyData()
		{
		}
	}
}
