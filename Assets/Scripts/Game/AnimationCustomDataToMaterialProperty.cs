using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000099; bodies translated from the 1.10.1 x86-64 binary.
	[ExecuteInEditMode]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class AnimationCustomDataToMaterialProperty : MonoBehaviour
	{
		[SerializeField]
		private AnimationCustomDataCharacter m_eyeData;

		[SerializeField]
		private Renderer m_renderer;

		[Tooltip("-1 does nothing.")]
		[SerializeField]
		private int m_targetMaterialIndex = -1;

		private const string LeftEyeHorizontalPropertyName = "_LeftEyeHorizontal";

		private const string LeftEyeVerticalPropertyName = "_LeftEyeVertical";

		private const string RightEyeHorizontalPropertyName = "_RightEyeHorizontal";

		private const string RightEyeVerticalPropertyName = "_RightEyeVertical";

		private const string LeftGlintHorizontalPropertyName = "_LeftGlintHorizontal";

		private const string LeftGlintVerticalPropertyName = "_LeftGlintVertical";

		private const string RightGlintHorizontalPropertyName = "_RightGlintHorizontal";

		private const string RightGlintVerticalPropertyName = "_RightGlintVertical";

		private readonly int m_leftEyeHorizontalHash = Shader.PropertyToID(LeftEyeHorizontalPropertyName);

		private readonly int m_leftEyeVerticalHash = Shader.PropertyToID(LeftEyeVerticalPropertyName);

		private readonly int m_rightEyeHorizontalHash = Shader.PropertyToID(RightEyeHorizontalPropertyName);

		private readonly int m_rightEyeVerticalHash = Shader.PropertyToID(RightEyeVerticalPropertyName);

		private readonly int m_leftGlintHorizontalHash = Shader.PropertyToID(LeftGlintHorizontalPropertyName);

		private readonly int m_leftGlintVerticalHash = Shader.PropertyToID(LeftGlintVerticalPropertyName);

		private readonly int m_rightGlintHorizontalHash = Shader.PropertyToID(RightGlintHorizontalPropertyName);

		private readonly int m_rightGlintVerticalHash = Shader.PropertyToID(RightGlintVerticalPropertyName);

		private Material m_material;

		// 0600051c (1.10.1 RVA 0x52E590): outside play mode in the editor the
		// shared material is driven directly instead of an instance.
		private void Start()
		{
			if (m_targetMaterialIndex < 0)
				return;
			var materials = new List<Material>(1);
			if (UseSharedMaterials())
				m_renderer.GetSharedMaterials(materials);
			else
				m_renderer.GetMaterials(materials);
			m_material = materials[m_targetMaterialIndex];
		}

		// 0600051d (1.10.1 RVA 0x52E760): only an instanced material is destroyed.
		private void OnDestroy()
		{
			if (!UseSharedMaterials() && m_material != null)
				Destroy(m_material);
		}

		// 0600051e (1.10.1 RVA 0x52E880)
		private void LateUpdate()
		{
			if (m_targetMaterialIndex < 0)
				return;
			m_material.SetFloat(m_leftEyeHorizontalHash, m_eyeData.eye_l_u);
			m_material.SetFloat(m_leftEyeVerticalHash, m_eyeData.eye_l_v);
			m_material.SetFloat(m_rightEyeHorizontalHash, m_eyeData.eye_r_u);
			m_material.SetFloat(m_rightEyeVerticalHash, m_eyeData.eye_r_v);
			m_material.SetFloat(m_leftGlintHorizontalHash, m_eyeData.glint_l_u);
			m_material.SetFloat(m_leftGlintVerticalHash, m_eyeData.glint_l_v);
			m_material.SetFloat(m_rightGlintHorizontalHash, m_eyeData.glint_r_u);
			m_material.SetFloat(m_rightGlintVerticalHash, m_eyeData.glint_r_v);
		}

		// 0600051f (1.10.1 RVA 0x52E960): only an index past the end is clamped.
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

		// 06000520 (1.10.1 RVA 0x52E6F0)
		private bool UseSharedMaterials()
		{
			return Application.isEditor && !Application.isPlaying;
		}

		// 06000521 (1.10.1 RVA 0x52EA40)
		public AnimationCustomDataToMaterialProperty()
		{
		}
	}
}
