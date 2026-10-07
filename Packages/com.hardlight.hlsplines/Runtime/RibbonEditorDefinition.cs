using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(menuName = "Hardlight/Splines/Ribbon Editor Definition")]
    public class RibbonEditorDefinition : SurfaceEditorDefinition
    {
        [SerializeField, Header("Calculation")] private float m_autoLengthUpdateInterval = 0.5f;
        [SerializeField, Header("Debug")] private bool m_disableRendering;
        [SerializeField] private bool m_displayAdjacentBlockKnotPositionCalculation;
        [SerializeField] private bool m_displayNearestBlockKnotPositionCalculation;
        [SerializeField] private Material m_visualisationMaterial;
        [SerializeField, Range(1f, 10f)] private int m_generatedMeshQuality = 1;
        [SerializeField] private string m_generatedMeshesLocation;
        [SerializeField] private float m_generatedMeshesSaveDelaySeconds = 1f;
        [Tooltip("When enabled, ribbons are occluded by other 3D geometry in the scene"), SerializeField]
        protected bool m_renderWithDepthTest;
        [SerializeField] private List<RibbonRenderQualitySetting> m_renderQualitySettings;
        private static RibbonEditorDefinition s_default;

        // Original 0x060001eb: a named default is the fallback; the first remaining asset overrides it.
        public static RibbonEditorDefinition Default
        {
            get
            {
                if (s_default == null)
                {
                    List<RibbonEditorDefinition> assets = new List<RibbonEditorDefinition>(Resources.LoadAll<RibbonEditorDefinition>(""));
                    RibbonEditorDefinition fallback = assets.Find(asset => asset.name.Contains("Default"));
                    assets.Remove(fallback);
                    if (assets.Count != 0)
                        fallback = assets[0];
                    s_default = fallback;
                }
                return s_default;
            }
        }

        public float AutoLengthUpdateInterval => m_autoLengthUpdateInterval; // 0x060001ec

        // Original 0x060001ed: the integer square may overflow; both distance tests are strict.
        private RibbonRenderQualitySetting GetRenderQualitySetting(bool isSelected, float distanceFromCamera)
        {
            List<RibbonRenderQualitySetting> settings = m_renderQualitySettings;
            if (settings.Count == 0)
                settings.Add(new RibbonRenderQualitySetting());
            RibbonRenderQualitySetting result = null;
            float currentDistance = float.MaxValue;
            foreach (RibbonRenderQualitySetting setting in m_renderQualitySettings)
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

        // Original 0x060001ee..0x060001f4.
        public bool DisplayAdjacentBlockKnotPositionCalculation => m_displayAdjacentBlockKnotPositionCalculation;
        public bool DisplayNearestBlockKnotPositionCalculation => m_displayNearestBlockKnotPositionCalculation;
        public Material VisualisationMaterial => m_visualisationMaterial;
        public int GeneratedMeshQuality => m_generatedMeshQuality;
        public string GeneratedMeshesLocation => m_generatedMeshesLocation;
        public float GeneratedMeshesSaveDelaySeconds => m_generatedMeshesSaveDelaySeconds;
        public bool RenderWithDepthTest => m_renderWithDepthTest;

        // Original 0x060001f5: retain the second Default read and the centre-only quality gate.
        public static bool ShouldRender(Vector3 location, bool isSelected, out RibbonRenderQualitySetting setting)
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

        // Original 0x060001f6 transforms only the centre and delegates, retaining the extents.
        public static bool ShouldRender(Bounds bounds, bool isSelected, out RibbonRenderQualitySetting setting, Matrix4x4 localToWorld)
        {
            if (Default.m_disableRendering)
            {
                setting = null;
                return false;
            }
            bounds.center = localToWorld.MultiplyPoint3x4(bounds.center);
            return ShouldRender(bounds, isSelected, out setting);
        }

        // Original 0x060001f7.
        public static bool ShouldRender(Bounds bounds, bool isSelected, out RibbonRenderQualitySetting setting)
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
    }
}
