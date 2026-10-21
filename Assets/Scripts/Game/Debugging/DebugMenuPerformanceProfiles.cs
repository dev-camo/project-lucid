using System.Collections.Generic;
using System.IO;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020005f4; bodies translated from the 1.10.1 x86-64 binary.
    // The DebugMenu.AddButton calls these methods build are compiled out of retail; only the loops and the
    // evaluations that precede them remain.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class DebugMenuPerformanceProfiles : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Reference ScalableFeatures that should be editable at runtime if using a custom performance profile")]
        private ScalableFeature[] m_scalableFeatures;

        private const string CustomProfileFileName = "UserPerformanceProfile";
        private const string CustomProfileFileExtension = ".json";

        // 06001ff3 (1.10.1 RVA 0x55E210): the native body folds the two constants into one literal.
        private static string CustomProfileFilePath =>
            Path.Combine(Application.persistentDataPath, CustomProfileFileName + CustomProfileFileExtension);

        // 06001ff4 (1.10.1 RVA 0x55E2A0): both callbacks run at once when the system is already valid.
        private void Start()
        {
            SystemRef<ScalablePerformance> scalablePerformanceRef = ProcessManager.GetSystemRef<ScalablePerformance>(null, true);
            scalablePerformanceRef.InvokeOnValid(AddDebugCustomPerformanceOptions);
            scalablePerformanceRef.InvokeOnValid(AddDebugPerformanceProfileSelection);
        }

        // 06001ff5 (1.10.1 RVA 0x55E3C0): each profile's name is read; the button built from it is compiled out.
        private void AddDebugPerformanceProfileSelection(ScalablePerformance scalablePerformance)
        {
            IReadOnlyList<PerformanceProfile> profiles = scalablePerformance.PerformanceProfiles;
            for (int i = 0; i < profiles.Count; i++)
            {
                _ = profiles[i].name;
            }
        }

        // 06001ff6 (1.10.1 RVA 0x55E510): the first custom profile in the list is used; with none, one is loaded
        // from the user file, given the serialized features when it has no attributes, and added to the list.
        public void AddDebugCustomPerformanceOptions(ScalablePerformance scalablePerformance)
        {
            PerformanceProfileCustom customProfile = null;
            IReadOnlyList<PerformanceProfile> profiles = scalablePerformance.PerformanceProfiles;
            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i] is PerformanceProfileCustom existing)
                {
                    customProfile = existing;
                    break;
                }
            }
            if (customProfile == null)
            {
                customProfile = PerformanceProfileCustom.LoadOrCreate(CustomProfileFilePath, CustomProfileFileName);
                if (customProfile.Attributes.Count == 0) customProfile.SetAttributes(m_scalableFeatures);
                scalablePerformance.AddPerformanceProfile(customProfile);
            }
            for (int i = 0; i < customProfile.Attributes.Count; i++)
            {
                // The per-attribute button is compiled out; the feature comparison it followed remains.
                _ = customProfile.Attributes[i]?.Feature == null;
            }
        }

        // 06001ff7 (1.10.1 RVA 0x55E8F0)
        private void UpdateCustomAttribute(PerformanceProfileCustom customProfile, PerformanceAttribute attribute)
        {
            customProfile.UpdateAttribute(attribute);
            customProfile.UpdateAndSaveProfile(CustomProfileFilePath);
        }

        // 06001ff8 (1.10.1 RVA 0x55E920)
        public DebugMenuPerformanceProfiles() { }
    }
}
