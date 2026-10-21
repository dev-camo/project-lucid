using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000938; bodies translated from the 1.10.1 x86-64 binary.
    // The language page: the chosen language is cached while the page is open and applied when it closes.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIContainerSettingsLanguage : UIContainer
    {
        [SerializeField] private UIWidgetSDTLanguageSelect[] m_languageSelectionButtons;
        private readonly SystemRef<App> m_appRef = ProcessManager.GetSystemRef<App>();
        private Languages m_languageToApply;

        // 0x06003501 (0x67D3D0): every button reports its choice to this page and shows the current language as chosen.
        private void Start()
        {
            m_languageToApply = Language.GetLanguage();
            foreach (UIWidgetSDTLanguageSelect button in m_languageSelectionButtons)
            {
                button.SelectionEvent += CacheLanguageChoice;
                button.SetCachedLanguage(m_languageToApply);
            }
        }

        // 0x06003502 (0x67D520)
        public void CacheLanguageChoice(Languages language)
        {
            m_languageToApply = language;
            foreach (UIWidgetSDTLanguageSelect button in m_languageSelectionButtons)
                button.SetCachedLanguage(language);
        }

        // 0x06003503 (0x67D570): the fonts are reported not ready for the new language. A changed language makes the
        // font loading report ready again; an unchanged one has no loading, so the report follows on the next frame.
        private void OnDestroy()
        {
            foreach (UIWidgetSDTLanguageSelect button in m_languageSelectionButtons)
                button.SelectionEvent -= CacheLanguageChoice;
            m_appRef.Get().Storage.SetValue(AppFSMKeys.FontsChangeLanguageReady, false);
            if (Language.OverrideLanguage(m_languageToApply))
                return;
            CoroutineUtils.OnNextFrame(() => ProcessManager.GetSystem<App>().Storage.SetValue(AppFSMKeys.FontsChangeLanguageReady, true));
        }

        // 0x06003504 (0x67D870)
        public UIContainerSettingsLanguage() { }
    }
}
