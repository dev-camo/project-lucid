using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Complete original HLUnityCore.Runtime 0200021c, 06000daa..06000db5.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class VisualQualityManager : MonoBehaviour, ISystem
    {
        [SerializeField] private ScalableFeature m_unityQualityConfig;
        [SerializeField] private UnityQualityConfiguration[] m_unityQualityConfigurations;
        [SerializeField] private ScalableFeature m_framerateConfig;
        [SerializeField] private FramerateConfiguration[] m_framerateConfigurations;
        [SerializeField] private ScalableFeature m_resolutionScaleConfig;
        [SerializeField] private ResolutionScaleConfiguration[] m_resolutionScaleConfigurations;
        private readonly SystemRef<ScalablePerformance> m_scalablePerformanceRef =
            ProcessManager.GetSystemRef<ScalablePerformance>(null, true);

        private void Awake()
        {
            ProcessManager.RegisterSystem(this, null, false, false);
            ProcessManager.SubscribeToAction(this, SystemAction.Initialise, OnInitialise);
            ProcessManager.SubscribeToAction(this, SystemAction.Shutdown, OnShutdown);
        }

        private void OnDestroy() => ProcessManager.UnregisterSystem(this);

        private void OnInitialise(object context = null)
        {
            m_scalablePerformanceRef.OnSystemStartup += ScalablePerformanceStartup;
            m_scalablePerformanceRef.OnSystemShutdown += OnScalablePerformanceShutdown;
            if (m_scalablePerformanceRef.IsValid())
                ScalablePerformanceStartup(m_scalablePerformanceRef.Get());
        }

        private void ScalablePerformanceStartup(ScalablePerformance scalablePerformance)
        {
            scalablePerformance.OnProfileUpdated += OnPerformanceProfileUpdated;
            OnPerformanceProfileUpdated(scalablePerformance.ActiveProfile);
        }

        private void OnScalablePerformanceShutdown(ScalablePerformance scalablePerformance) =>
            scalablePerformance.OnProfileUpdated -= OnPerformanceProfileUpdated;

        private void OnShutdown(object context = null)
        {
            if (m_scalablePerformanceRef.IsValid())
                OnScalablePerformanceShutdown(m_scalablePerformanceRef.Get());
        }

        protected virtual void OnPerformanceProfileUpdated(PerformanceProfile profile)
        {
            UpdateVisualFeature(profile, m_unityQualityConfig, m_unityQualityConfigurations);
            UpdateVisualFeature(profile, m_framerateConfig, m_framerateConfigurations);
            UpdateVisualFeature(profile, m_resolutionScaleConfig, m_resolutionScaleConfigurations);
        }

        protected void UpdateVisualFeature<T>(PerformanceProfile profile, ScalableFeature feature,
            T[] configurations) where T : VisualQualityConfiguration
        {
            PerformanceAttribute attribute = profile.GetAttribute(feature);
            PerformanceProfile.QualityLevel quality = attribute == null
                ? PerformanceProfile.QualityLevel.Medium : attribute.Level;
            GetConfiguration(configurations, quality).Apply();
        }

        private T GetConfiguration<T>(T[] configurations, PerformanceProfile.QualityLevel quality)
            where T : VisualQualityConfiguration
        {
            // The first authored configuration is captured before the search.
            // No empty-array fallback or null-entry filtering existed here.
            T configuration = configurations[0];
            int index = Array.FindIndex(configurations, match => match.QualityLevel == quality);
            if (index >= 0)
                configuration = configurations[index];
            return configuration;
        }

        public VisualQualityManager() { }
    }
}
