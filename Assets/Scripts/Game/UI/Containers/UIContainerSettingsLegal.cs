using Hardlight;
using Hardlight.Analytics;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200093a; bodies translated from the 1.10.1 x86-64 binary.
    // The legal page: which of its buttons are offered when it opens.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIContainerSettingsLegal : UIContainer
    {
        [SerializeField] private UIContainerIdentifier m_levelSelectIdentifier;
        [SerializeField] private UIContainerIdentifier m_mainMenuIdentifier;
        public readonly Bindable<bool> CanShowAnalyticsConsentButton = new Bindable<bool>();
        public readonly Bindable<bool> CanShowCreditsButton = new Bindable<bool>();

        // 0x06003508 (0x67DAD0): the credits are offered from the level select or the main menu only.
        private void OnEnable()
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active) // Offline hook H-067: the original resolved the consent manager and asked it; offline none is registered, so the consent button is hidden (the original statements stay in the else branch)
            {
                CanShowAnalyticsConsentButton.Value = ProjectLucid.Offline.OfflineAnalyticsConsumers.CanShowConsentScreenButtonOnSettings();
            }
            else
            {
                AnalyticsConsentManager consentManager = ProcessManager.GetSystem<AnalyticsConsentManager>();
                CanShowAnalyticsConsentButton.Value = consentManager.CanShowConsentScreenButtonOnSettings();
            }
            UIManager uiManager = ProcessManager.GetSystem<UIManager>();
            CanShowCreditsButton.Value = uiManager.IsOpen(m_levelSelectIdentifier) || uiManager.IsOpen(m_mainMenuIdentifier);
        }

        // 0x06003509 (0x67DBD0)
        public UIContainerSettingsLegal() { }
    }
}
