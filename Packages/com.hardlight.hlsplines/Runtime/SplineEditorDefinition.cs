using Unity.IL2CPP.CompilerServices;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight
{
	// Original HLSplines.Runtime 02000060; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(menuName = "Hardlight/Splines/Spline Editor Definition")]
	public class SplineEditorDefinition : SurfaceEditorDefinition
	{
		[Header("Calculation")]
		[SerializeField]
		private float m_autoLengthUpdateInterval = 0.5f;

		[SerializeField]
		[Header("Debug")]
		private bool m_disableRendering;

		[SerializeField]
		[Range(1f, 10f)]
		private int m_generatedMeshQuality = 1;

		[SerializeField]
		[Range(0.01f, 1f)]
		private float m_generatedMeshWidth = 0.1f;

		[SerializeField]
		private Material m_visualisationMaterial;

		[SerializeField]
		private string m_generatedMeshesLocation;

		[SerializeField]
		private float m_generatedMeshesSaveDelaySeconds = 1f;

		[SerializeField]
		private bool m_snapToMesh = true;

		[SerializeField]
		private float m_meshSnapDistance = 5f;

		[SerializeField]
		[Tooltip("When enabled, splines are occluded by other 3D geometry in the scene")]
		protected bool m_renderWithDepthTest;

		[Header("Display")]
		[SerializeField]
		private List<SplineRenderQualitySetting> m_renderQualitySettings;

		private static SplineEditorDefinition s_default;

		// 0600020a (1.10.1 RVA 0x1A71CA0): a named default is the fallback; the first remaining asset overrides it.
		public static SplineEditorDefinition Default
		{
			get
			{
				if (s_default == null)
				{
					List<SplineEditorDefinition> assets = new List<SplineEditorDefinition>(Resources.LoadAll<SplineEditorDefinition>(""));
					SplineEditorDefinition fallback = assets.Find(asset => asset.name.Contains("Default"));
					assets.Remove(fallback);
					if (assets.Count != 0)
						fallback = assets[0];
					s_default = fallback;
				}
				return s_default;
			}
		}

		// 0600020b..06000213 (1.10.1 RVA 0x1A71F10..0x1A71F90)
		public float AutoLengthUpdateInterval => m_autoLengthUpdateInterval;
		public Material VisualisationMaterial => m_visualisationMaterial;
		public int GeneratedMeshQuality => m_generatedMeshQuality;
		public string GeneratedMeshesLocation => m_generatedMeshesLocation;
		public float GeneratedMeshWidth => m_generatedMeshWidth;
		public float GeneratedMeshesSaveDelaySeconds => m_generatedMeshesSaveDelaySeconds;
		public bool SnapToMesh => m_snapToMesh;
		public float MeshSnapDistance => m_meshSnapDistance;
		public bool RenderWithDepthTest => m_renderWithDepthTest;

		// 06000214 (1.10.1 RVA 0x1A71FA0): the integer square may overflow; both distance tests are strict.
		private SplineRenderQualitySetting GetRenderQualitySetting(bool isSelected, float distanceFromCamera)
		{
			List<SplineRenderQualitySetting> settings = m_renderQualitySettings;
			if (settings.Count == 0)
				settings.Add(new SplineRenderQualitySetting());
			SplineRenderQualitySetting result = null;
			float currentDistance = float.MaxValue;
			foreach (SplineRenderQualitySetting setting in m_renderQualitySettings)
			{
				if (setting.IsSelected == isSelected)
				{
					float maxDistance = unchecked(setting.SplineDrawDistance * setting.SplineDrawDistance);
					if (maxDistance > distanceFromCamera && currentDistance > maxDistance)
					{
						currentDistance = maxDistance;
						result = setting;
					}
				}
			}
			return result;
		}

		// 06000215 (1.10.1 RVA 0x1A721B0): Default is read again for the quality lookup.
		public static bool ShouldRender(Vector3 location, bool isSelected, out SplineRenderQualitySetting setting)
		{
			if (Default.m_disableRendering)
			{
				setting = null;
				return false;
			}
			float distance = GetDistanceFromCamera(location);
			if (distance < 0f)
			{
				setting = null;
				return false;
			}
			setting = Default.GetRenderQualitySetting(isSelected, distance);
			return setting != null && setting.CentreSpline.RenderQuality != 0;
		}

		// 06000216 (1.10.1 RVA 0x1A72250): transforms only the centre and delegates, keeping the extents.
		public static bool ShouldRender(Bounds bounds, bool isSelected, out SplineRenderQualitySetting setting, Matrix4x4 localToWorld)
		{
			if (Default.m_disableRendering)
			{
				setting = null;
				return false;
			}
			bounds.center = localToWorld.MultiplyPoint3x4(bounds.center);
			return ShouldRender(bounds, isSelected, out setting);
		}

		// 06000217 (1.10.1 RVA 0x1A722F0)
		public static bool ShouldRender(Bounds bounds, bool isSelected, out SplineRenderQualitySetting setting)
		{
			if (Default.m_disableRendering)
			{
				setting = null;
				return false;
			}
			float distance = GetDistanceFromCamera(bounds);
			if (distance < 0f)
			{
				setting = null;
				return false;
			}
			setting = Default.GetRenderQualitySetting(isSelected, distance);
			return setting != null && setting.CentreSpline.RenderQuality != 0;
		}

		// 06000218 (1.10.1 RVA 0x1A723A0)
		public SplineEditorDefinition()
		{
		}
	}
}
