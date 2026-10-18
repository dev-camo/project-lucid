using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020000a2; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class AnimatorToMaterialProperty : MonoBehaviour
	{
		[SerializeField]
		private Animator m_animator;

		[SerializeField]
		private Renderer m_renderer;

		[SerializeField]
		private AnimatorToMaterialPropertyData m_propertyData;

		[Header("Leave as -1 to update all materials.")]
		[SerializeField]
		private int m_targetMaterialIndex = -1;

		private List<Material> m_materials = new List<Material>(1);

		// 06000539 (1.10.1 RVA 0x52F290): the renderer's instanced materials.
		private void Start()
		{
			m_renderer.GetMaterials(m_materials);
		}

		// 0600053a (1.10.1 RVA 0x52F2B0)
		private void OnDestroy()
		{
			foreach (Material material in m_materials)
				Destroy(material);
			m_materials = null;
		}

		// 0600053b (1.10.1 RVA 0x52F420): a pair is skipped while its parameter hash
		// is still zero or its material property name is empty.
		private void LateUpdate()
		{
			foreach (AnimatorToMaterialPropertyData.ParameterToPropertyPair pair in m_propertyData.ParametersToProperties)
			{
				if (pair.AnimParameterHash == 0 || string.IsNullOrEmpty(pair.MaterialProperty))
					continue;
				float value = m_animator.GetFloat(pair.AnimParameterHash);
				if (m_targetMaterialIndex >= 0)
				{
					m_materials[m_targetMaterialIndex].SetFloat(pair.MaterialPropertyHash, value);
				}
				else
				{
					foreach (Material material in m_materials)
						material.SetFloat(pair.MaterialPropertyHash, value);
				}
			}
		}

		// 0600053c (1.10.1 RVA 0x52F620): only an index past the end is clamped.
		private void OnValidate()
		{
			if (gameObject.CanValidate() && m_renderer != null)
			{
				int materialCount = m_renderer.sharedMaterials.Length;
				if (m_targetMaterialIndex >= materialCount)
				{
					m_targetMaterialIndex = materialCount - 1;
					HLOutput.LogError("Target material index is out of bounds for the target renderer.", this);
				}
			}
		}

		// 0600053d (1.10.1 RVA 0x52F700)
		public AnimatorToMaterialProperty()
		{
		}
	}
}
