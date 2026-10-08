using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Complete original HLUnityCore.Runtime 02000218, 06000d97..06000da4.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ScalablePerformance : MonoBehaviour, ISystem
    {
        public const string AssetMenuRoot = "Hardlight/HLUnityCore/ScalablePerformance/";
        public event Action<PerformanceProfile> OnProfileUpdated;
        [SerializeField] private List<PerformanceProfile> m_performanceProfiles;
        [SerializeField] private PerformanceProfile m_defaultSetting;
        private PerformanceProfile m_activeProfile;

        public PerformanceProfile ActiveProfile => m_activeProfile;
        public IReadOnlyList<PerformanceProfile> PerformanceProfiles => m_performanceProfiles;

        private void Awake()
        {
            ProcessManager.RegisterSystem(this, null, false, false);
            SetPerformanceProfile(m_defaultSetting);
        }

        private void OnDestroy()
        {
            if (m_activeProfile != null)
                m_activeProfile.OnProfileUpdated -= OnPerformanceProfileEdited;
            ProcessManager.UnregisterSystem(this);
        }

        public void SetPerformanceProfile(string profileName) =>
            SetPerformanceProfile(GetPerformanceProfile(profileName));

        public void SetPerformanceProfile(PerformanceProfile profile)
        {
            // Equality belongs to ScriptableObjectWithGuid. A profile that is
            // equal by that original identity does not trigger a notification.
            if (profile == m_activeProfile)
                return;
            if (m_activeProfile != null)
                m_activeProfile.OnProfileUpdated -= OnPerformanceProfileEdited;
            m_activeProfile = profile;
            // Retain the original store-before-subscribe order and null fault.
            m_activeProfile.OnProfileUpdated += OnPerformanceProfileEdited;
            OnProfileUpdated?.Invoke(m_activeProfile);
        }

        public void AddPerformanceProfile(PerformanceProfile profile) =>
            m_performanceProfiles.Add(profile);

        private void OnPerformanceProfileEdited() => SetPerformanceProfile(m_activeProfile);

        public PerformanceProfile GetPerformanceProfile(string profileName)
        {
            bool NameMatch(PerformanceProfile profile) => string.Equals(profile.name, profileName);
            return m_performanceProfiles.Find(NameMatch);
        }

        public ScalablePerformance() { }
    }
}
