using Hardlight.Enums;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight.Localisation
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class Language : MonoSingleton<Language>
    {
        public delegate void LanguageChangedHandler(Languages language);

        [SerializeField]
        private Languages m_defaultLanguage = Languages.EnglishUS;
        [SerializeField]
        private bool m_forceDefaultLanguage;
        private bool m_languageLoaded;
        private bool m_overrideLanguage;
        private string m_osLanguageString = string.Empty;
        private Languages m_currentLanguage = Languages.EnglishUS;

        // HLLocalisation.Runtime 0x06000010..0x06000013, original field getters.
        public bool LanguageLoaded => m_languageLoaded;
        public string OSLanguageString => m_osLanguageString;
        public Languages CurrentLanguage => m_currentLanguage;
        public Languages DefaultLanguage => m_defaultLanguage;

        // 0x06000014, ARM64 0x1a39af8. Preserve Unity equality and the
        // second current-singleton lookup after the null comparison.
        public static int GetLanguageAsInt()
        {
            return Instance != null ? (int)Instance.m_currentLanguage : (int)Languages.EnglishUS;
        }

        // 0x06000015, ARM64 0x1a39c2c.
        public static Languages GetLanguage()
        {
            return Instance != null ? Instance.m_currentLanguage : Languages.EnglishUS;
        }

        // 0x06000016, ARM64 0x1a39d60. Original has no singleton-null guard.
        public static bool IsLanguageOverriden() => Instance.m_overrideLanguage;

        // 0x06000017..18: original CompilerGenerated backing field/accessors;
        // delegate Combine/Remove plus atomic CompareExchange retry loops.
        public static event LanguageChangedHandler OnLanguageChanged;

        private const string languageOverrideSaveKey = "language_override";
        private const string languageCurrentSaveKey = "language_current";

        // 0x06000019, ARM64 0x1a39f9c. Equality leaves override state unchanged.
        // The return comparison uses the old value even after reentrant callbacks.
        public static bool OverrideLanguage(Languages language)
        {
            Languages previous = Instance.m_currentLanguage;
            if (previous != language)
            {
                Instance.m_overrideLanguage = true;
                Instance.m_currentLanguage = language;
                OnLanguageChanged?.Invoke(language);
            }
            return previous != language;
        }

        // 0x0600001a, ARM64 0x1a3a138. Clear before native country/language calls;
        // field accesses continue to use the current singleton after calculation.
        public static void ClearOverrideLanguage()
        {
            Instance.m_overrideLanguage = false;
            Languages language = Instance.CalculateSystemLanguage();
            if (language != Instance.m_currentLanguage)
            {
                Instance.m_currentLanguage = language;
                OnLanguageChanged?.Invoke(language);
            }
        }

        // 0x0600001b, ARM64 0x1a3a3d8. The load handler may run synchronously;
        // inspect its updated override flag after both subscriptions are added.
        private void Start()
        {
            HLPropertyStore.AddLoadHandler(OnPropertyStoreLoad, true);
            HLPropertyStore.AddSaveHandler(OnPropertyStoreSave);
            if (!m_overrideLanguage)
                m_currentLanguage = CalculateSystemLanguage();
        }

        // 0x0600001c, ARM64 0x1a3a508. Remove both subscriptions before base cleanup.
        protected override void OnDestroy()
        {
            HLPropertyStore.RemoveLoadHandler(OnPropertyStoreLoad);
            HLPropertyStore.RemoveSaveHandler(OnPropertyStoreSave);
            base.OnDestroy();
        }

        // 0x0600001d, ARM64 0x1a3a630. Original RGCTX is AddProperty<bool>,
        // then AddProperty<string>; read current language after the bool property.
        private void OnPropertyStoreSave(HLPropertyList propertyList)
        {
            propertyList.AddProperty<bool>(languageOverrideSaveKey, m_overrideLanguage);
            propertyList.AddProperty<string>(languageCurrentSaveKey, m_currentLanguage.GetString());
        }

        // 0x0600001e, ARM64 0x1a3a724. Original SafeParse<Languages>; isNewFile
        // is unused. Nonoverride loads retain current language. Mark loaded only
        // after the notification returns, preserving exception/reentrant ordering.
        private void OnPropertyStoreLoad(HLPropertyList propertyList, bool isNewFile)
        {
            bool wasLoaded = m_languageLoaded;
            m_overrideLanguage = propertyList.AsBool(languageOverrideSaveKey, false);
            bool notify = !wasLoaded;
            if (m_overrideLanguage)
            {
                Languages language = EnumUtilities.SafeParse<Languages>(
                    propertyList.AsString(languageCurrentSaveKey, null), m_defaultLanguage);
                if (language != m_currentLanguage)
                {
                    m_currentLanguage = language;
                    notify = true;
                }
            }
            if (notify)
                OnLanguageChanged?.Invoke(m_currentLanguage);
            m_languageLoaded = true;
        }

        // 0x0600001f, ARM64 0x1a3a308. Keep Application before country lookup,
        // culture-sensitive ToLower and the original absence of a null fallback.
        private Languages CalculateSystemLanguage()
        {
            SystemLanguage unityLanguage = Application.systemLanguage;
            m_osLanguageString = unityLanguage.ToString();
            string country = HLUnityCore.GetDeviceISO2CountryCode().ToLower();
            return ConvertLanguage(unityLanguage, country);
        }

        // 0x06000020, ARM64 0x1a3a848. Recovered42-entry native switch at
        // 0x2a7dba8; country literals are exactly "gb" then "au". Country is
        // consulted only for English. The serialized force-default flag is not
        // read by this original player implementation.
        private Languages ConvertLanguage(SystemLanguage unityLanguage, string iso2CountryCode)
        {
            switch (unityLanguage)
            {
                case SystemLanguage.Afrikaans: return Languages.Afrikaans;
                case SystemLanguage.Arabic: return Languages.Arabic;
                case SystemLanguage.Bulgarian: return Languages.Bulgarian;
                case SystemLanguage.Catalan: return Languages.Catalan;
                case SystemLanguage.Chinese: return Languages.ChineseSimplified;
                case SystemLanguage.Czech: return Languages.Czech;
                case SystemLanguage.Danish: return Languages.Danish;
                case SystemLanguage.Dutch: return Languages.Dutch;
                case SystemLanguage.English:
                    if (iso2CountryCode == "gb") return Languages.EnglishUK;
                    if (iso2CountryCode == "au") return Languages.EnglishAus;
                    return Languages.EnglishUS;
                case SystemLanguage.Estonian: return Languages.Estonian;
                case SystemLanguage.Finnish: return Languages.Finnish;
                case SystemLanguage.French: return Languages.French;
                case SystemLanguage.German: return Languages.German;
                case SystemLanguage.Greek: return Languages.Greek;
                case SystemLanguage.Hebrew: return Languages.Hebrew;
                case SystemLanguage.Hungarian: return Languages.Hungarian;
                case SystemLanguage.Indonesian: return Languages.Indonesian;
                case SystemLanguage.Italian: return Languages.Italian;
                case SystemLanguage.Japanese: return Languages.Japanese;
                case SystemLanguage.Korean: return Languages.Korean;
                case SystemLanguage.Latvian: return Languages.Latvian;
                case SystemLanguage.Lithuanian: return Languages.Lithuanian;
                case SystemLanguage.Norwegian: return Languages.Norwegian;
                case SystemLanguage.Polish: return Languages.Polish;
                case SystemLanguage.Portuguese: return Languages.PortugueseBrazil;
                case SystemLanguage.Romanian: return Languages.Romanian;
                case SystemLanguage.Russian: return Languages.Russian;
                case SystemLanguage.SerboCroatian: return Languages.SerbianLatin;
                case SystemLanguage.Slovak: return Languages.Slovak;
                case SystemLanguage.Slovenian: return Languages.Slovenian;
                case SystemLanguage.Spanish: return Languages.Spanish;
                case SystemLanguage.Swedish: return Languages.Swedish;
                case SystemLanguage.Thai: return Languages.Thai;
                case SystemLanguage.Turkish: return Languages.Turkish;
                case SystemLanguage.Ukrainian: return Languages.Ukrainian;
                case SystemLanguage.Vietnamese: return Languages.Vietnamese;
                case SystemLanguage.ChineseSimplified: return Languages.ChineseSimplified;
                case SystemLanguage.ChineseTraditional: return Languages.ChineseTraditional;
                default: return m_defaultLanguage;
            }
        }

        // 0x06000021, ARM64 0x1a3ac8c. Ordered field initializers precede the
        // genuine MonoSingleton base constructor; no added lifecycle behavior.
        public Language() { }
    }
}
