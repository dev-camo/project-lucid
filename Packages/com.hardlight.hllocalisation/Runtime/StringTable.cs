using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Hardlight.Enums;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Networking;

namespace Hardlight.Localisation
{
    // Original HLLocalisation.Runtime 0x0200000f. Native-derived candidate;
    // original content/bootstrap/runtime parity still requires separate proof.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class StringTable : MonoSingleton<StringTable>
    {
        public static FastAction StringsTableHandlers;
        public static event Func<bool> ShouldInvokeStringHandlers = () => true;
        public static string Hash { get; private set; }
        public static Languages StringsLanguage { get; private set; }
        public const string PlusOrMinusNoDecimalFormat = "{0:+0;-0;0}";
        public const string PlusOrMinusOneDecimalFormat = "{0:+0.#;-0.#;0}";
        public const string PlusOrMinusTwoDecimalFormat = "{0:+0.##;-0.##;0}";
        public const string PlusOrMinusThreeDecimalFormat = "{0:+0.###;-0.###;0}";
        private Coroutine m_coroutineDelayingStringHandlerInvocation;
        public NumberFormatInfo NumberFormat { get; private set; }
        public static event Action OnRequestStringTable;
        private ClientDataAPI.SupportedLanguage[] m_supportedLanguages;
        private Dictionary<int, StringEntry> m_strings;
        private long m_cachedAtTime;
        private const string StringTableCachedTimeSaveProperty = "StringTableCachedTime";
        private const string CacheFilePrefix = "Text-";
        private const string CacheVersionPostfix = "-Version";
        private HLLocalisationConfigurationAsset m_config;

        public struct StringEntry
        {
            public readonly string id;
            public readonly string content;
            public readonly int numArgs;

            // 0x06000070; ARM64 0x1a3ef28.
            public StringEntry(string id, string content, int numArgs)
            {
                this.id = id;
                this.content = content;
                this.numArgs = numArgs;
            }
        }

        // 0x0600004a; ARM64 0x1a3ce48. Return the owned array, not a copy.
        public static ClientDataAPI.SupportedLanguage[] GetSupportedLanguages() => Instance.m_supportedLanguages;

        // 0x0600004b; ARM64 0x1a3c6bc. Re-read the original owner on each access.
        public static ClientDataAPI.SupportedLanguage GetCurrentLanguageData()
        {
            for (int i = 0; i < Instance.m_supportedLanguages.Length; i++)
                if (Instance.m_supportedLanguages[i].Language == Language.GetLanguageAsInt())
                    return Instance.m_supportedLanguages[i];
            return null;
        }

        // 0x0600004c; ARM64 0x1a3cf04.
        public static bool StringExists(Strings stringId) =>
            Instance != null && Instance.m_strings != null && Instance.m_strings.ContainsKey((int)stringId);

        // 0x0600004d; ARM64 0x1a3b320. NONE bypasses lookup, even if stored.
        public static string GetString(Strings stringId)
        {
            if (stringId == Strings.NONE || Instance == null || Instance.m_strings == null)
                return string.Empty;
            if (Instance.m_strings.TryGetValue((int)stringId, out StringEntry entry))
                return entry.content;
            return GetMissingString(stringId);
        }

        // 0x0600004e; ARM64 0x1a3d0bc.
        public static string GetString(string stringId) => GetString((Strings)GetStringID(stringId));

        // 0x0600004f; ARM64 0x1a3bcd0. Unlike GetString, NONE is not special.
        public static bool GetStringAndNumArgs(Strings stringId, out string content, out int numArgs)
        {
            if (Instance != null && Instance.m_strings != null &&
                Instance.m_strings.TryGetValue((int)stringId, out StringEntry entry))
            {
                content = entry.content;
                numArgs = entry.numArgs;
                return true;
            }
            content = GetMissingString(stringId);
            numArgs = 0;
            return false;
        }

        // 0x06000050; ARM64 0x1a3d214.
        public static bool GetStringAndNumArgs(string stringId, out string content, out int numArgs) =>
            GetStringAndNumArgs((Strings)GetStringID(stringId), out content, out numArgs);

        // 0x06000051; ARM64 0x1a3d2fc. Literal slash is part of the original path.
        public static string GetCachePath(string languageString) =>
            Application.persistentDataPath + "/" + CacheFilePrefix + languageString;

        // 0x06000052; ARM64 0x1a3d380.
        public static string GetCachePathVersion(string languageString) => GetCachePath(languageString) + CacheVersionPostfix;

        // 0x06000053; ARM64 0x1a3d480. The original base declines cache reads.
        protected virtual bool IsDirty() => false;

        // 0x06000054; ARM64 0x1a3d488. Unwind tables distinguish the two
        // independent catches. Decode and the custom-key callback are outside
        // both catches; a malformed codec buffer is not a fallback condition.
        public static ClientDataAPI.LocalisationDefinitions LoadLanguageDefinition(
            Languages language, string directory, Action<ClientDataAPI.LocalisationDefinitions> callback = null)
        {
            byte[] data = null;
            if (Application.isPlaying)
            {
                ProcessManager.GetSystem<HLUnityCore>().CustomKeyString("Language", language.GetString());
                try
                {
                    data = LoadLocalisationDefinitionFromCache(language);
                }
                catch (Exception)
                {
                    // The native cache exception is discarded before trying the supplied file.
                }
            }
            if (data == null)
            {
                string filePath = string.Empty;
                try
                {
                    filePath = GetStreamingAssetsLanguagePath(language, directory);
                    using (FileStream stream = File.OpenRead(filePath))
                    {
                        BinaryReader reader = new BinaryReader(stream);
                        data = reader.ReadBytes(unchecked((int)stream.Length));
                    }
                }
                catch (Exception error)
                {
                    if (language == Language.Instance.DefaultLanguage)
                    {
                        HLOutput.LogError(string.Format(
                            "Unable to load strings file for default language {0} at path: '{1}' with error '{2}'. No fallback available.",
                            language, filePath, error.Message));
                        return null;
                    }
                    AreStringsLoaded();
                    return LoadLanguageDefinition(Language.Instance.DefaultLanguage, directory, callback);
                }
            }
            return ClientDataAPI.LocalisationDefinitions.decode(data);
        }

        // 0x06000055; ARM64 0x1a3dcdc. Path.Combine preserves rooted-directory semantics.
        private static string GetStreamingAssetsLanguagePath(Languages language, string directory) =>
            Path.Combine(Application.streamingAssetsPath, directory, language.GetString() + ".bytes");

        // 0x06000056; ARM64 0x1a3d18c.
        public static int GetStringID(string stringName) => HLCRC32.GenerateInt(stringName);

        // 0x06000057; ARM64 0x1a3ddd8.
        public static bool AreStringsLoaded() => Instance != null && Instance.m_strings != null;

        // 0x06000058; ARM64 0x1a3df38. Immediate store-load callback precedes
        // save registration and language subscription, exactly as in the original.
        protected void Start()
        {
            m_config = SystemConfiguration.GetConfig<HLLocalisationConfigurationAsset>();
            HLPropertyStore.AddLoadHandler(OnPropertyStoreLoad, true);
            HLPropertyStore.AddSaveHandler(OnPropertyStoreSave);
            Language.OnLanguageChanged += OnLanguageChanged;
            if (Language.Instance.LanguageLoaded) OnLanguageChanged(Language.GetLanguage());
        }

        // 0x06000059; ARM64 0x1a3e374. Original body does not stop the deferred coroutine.
        protected override void OnDestroy()
        {
            HLPropertyStore.RemoveLoadHandler(OnPropertyStoreLoad);
            HLPropertyStore.RemoveSaveHandler(OnPropertyStoreSave);
            Language.OnLanguageChanged -= OnLanguageChanged;
            base.OnDestroy();
        }

        // 0x0600005a; ARM64 0x1a3e5e4.
        private void OnPropertyStoreSave(HLPropertyList propertyList) => propertyList.AddProperty<long>(StringTableCachedTimeSaveProperty, m_cachedAtTime);

        // 0x0600005b; ARM64 0x1a3e660. isNewFile is unused.
        private void OnPropertyStoreLoad(HLPropertyList propertyList, bool isNewFile) => m_cachedAtTime = propertyList.AsLong(StringTableCachedTimeSaveProperty, 0);

        // 0x0600005c; ARM64 0x1a3e27c. The synchronous return is applied only
        // when non-null; the supplied callback belongs to the async route.
        private void OnLanguageChanged(Languages language)
        {
            ClientDataAPI.LocalisationDefinitions definition = LoadLanguageDefinition(
                language, m_config.LocalisedDefinitionsDirectory, OnLanguageDefinitionLoaded);
            if (definition != null) OnLanguageDefinitionLoaded(definition);
        }

        // 0x0600005d; ARM64 0x1a3e6c8.
        private void OnLanguageDefinitionLoaded(ClientDataAPI.LocalisationDefinitions definition)
        {
            m_supportedLanguages = definition.SupportedLanguages;
            InternalSetStrings(ReadStringsFromBuffer(definition.StringTable));
            if (ShouldInvokeStringHandlers()) InvokeStringTableHandlers(definition);
            else
            {
                this.SafeStopCoroutine(ref m_coroutineDelayingStringHandlerInvocation);
                m_coroutineDelayingStringHandlerInvocation = StartCoroutine(WaitOnProjectAssets(definition));
            }
        }

        // 0x0600005e and original <WaitOnProjectAssets>d__52 six methods.
        private IEnumerator WaitOnProjectAssets(ClientDataAPI.LocalisationDefinitions definition)
        {
            while (!ShouldInvokeStringHandlers()) yield return null;
            InvokeStringTableHandlers(definition);
        }

        // 0x0600005f; ARM64 0x1a3eb5c. Handlers run before the live request event.
        private void InvokeStringTableHandlers(ClientDataAPI.LocalisationDefinitions definition)
        {
            StringsLanguage = (Languages)definition.StringTable.Language;
            StringsTableHandlers.Invoke();
            RequestStringTable();
        }

        // 0x06000060 and original <DelayedStringLoad>d__54 six methods.
        protected virtual IEnumerator DelayedStringLoad()
        {
            OnLanguageChanged(Language.GetLanguage());
            yield return null;
        }

        // 0x06000061; ARM64 0x1a3ee98.
        private void InternalSetSupportedLanguages(ClientDataAPI.SupportedLanguage[] supportedLanguages) => m_supportedLanguages = supportedLanguages;

        // 0x06000062; ARM64 0x1a3ea28. No separator defaults or sanitization:
        // the actual table lookups feed the BCL setters and their exceptions.
        private void InternalSetStrings(Dictionary<int, StringEntry> strings)
        {
            m_strings = strings;
            NumberFormat = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
            NumberFormat.NumberGroupSeparator = GetString(Strings.NUMBER_SEPARATOR);
            NumberFormat.NumberDecimalSeparator = GetString(Strings.DECIMAL_SEPARATOR);
        }

        // 0x06000063 and original <WaitForStrings>d__57 six methods.
        public static IEnumerator WaitForStrings()
        {
            while (Instance == null || Instance.m_strings == null) yield return null;
        }

        // 0x06000064; ARM64 0x1a3e820. Hash changes before array validation;
        // duplicate hashes overwrite previous entries, with live array reads.
        public static Dictionary<int, StringEntry> ReadStringsFromBuffer(ClientDataAPI.StringTable stringTable)
        {
            Hash = stringTable.Hash;
            int numStrings = stringTable.Strings.Length;
            Dictionary<int, StringEntry> result = new Dictionary<int, StringEntry>(numStrings);
            for (int i = 0; i < numStrings; i++)
            {
                ClientDataAPI.LocalisedString value = stringTable.Strings[i];
                int key = GetStringID(value.Id);
                result[key] = new StringEntry(value.Id, value.Content, value.NumArgs);
            }
            return result;
        }

        // 0x06000065; ARM64 0x1a3ef70. Native catches System.Exception and
        // silently returns after partial mutations; it does not roll back.
        public void UpdateLocalisationDefinition(byte[] localisationDefinitionData)
        {
            if (localisationDefinitionData == null) return;
            try
            {
                ClientDataAPI.LocalisationDefinitions definition = ClientDataAPI.LocalisationDefinitions.decode(localisationDefinitionData);
                if (definition != null) UpdateLocalisationDefinitionInternal(definition, localisationDefinitionData);
            }
            catch (Exception) { }
        }

        // 0x06000066; ARM64 0x1a3f0cc. Preserve the original reset encoding
        // singleton call instead of substituting definition.encode().
        public void UpdateLocalisationDefinition(ClientDataAPI.LocalisationDefinitions definition)
        {
            if (definition == null) return;
            try
            {
                byte[] data = ClientDataAPI.LocalisationDefinitions.GetEncodingInstance().encode();
                if (data != null) UpdateLocalisationDefinitionInternal(definition, data);
            }
            catch (Exception) { }
        }

        // 0x06000067; ARM64 0x1a3f08c. Re-read language after callbacks return.
        private void UpdateLocalisationDefinitionInternal(ClientDataAPI.LocalisationDefinitions localisationDefinition, byte[] localisationDefinitionBuffer)
        {
            OnLanguageDefinitionLoaded(localisationDefinition);
            SaveLocalisationDefinitionToCache((Languages)localisationDefinition.StringTable.Language, localisationDefinitionBuffer);
        }

        // 0x06000068 plus original <LoadLocalisationDefinitions>d__62 seven
        // methods. Request remains live through callback and nested fallback;
        // using produces the original disposal/fault path without swallowing.
        private static IEnumerator LoadLocalisationDefinitions(Languages language, string directory, Action<ClientDataAPI.LocalisationDefinitions> callback)
        {
            string filePath = GetStreamingAssetsLanguagePath(language, directory);
            using (UnityWebRequest webRequest = UnityWebRequest.Get(filePath))
            {
                yield return webRequest.SendWebRequest();
                if (string.IsNullOrEmpty(webRequest.error))
                {
                    ClientDataAPI.LocalisationDefinitions localisationDefinition = ClientDataAPI.LocalisationDefinitions.decode(webRequest.downloadHandler.data);
                    callback(localisationDefinition);
                }
                else if (language == Language.Instance.DefaultLanguage)
                    HLOutput.LogError(string.Format(
                        "Unable to load strings file for default language {0} at path: '{1}' with error '{2}'. No fallback available.",
                        language, filePath, webRequest.error));
                else
                {
                    AreStringsLoaded();
                    yield return LoadLocalisationDefinitions(Language.Instance.DefaultLanguage, directory, callback);
                }
            }
        }

        // 0x06000069; ARM64 0x1a3f1f0. Write bytes before invoking the live
        // client-version override. Failures are discarded without undoing IO.
        private void SaveLocalisationDefinitionToCache(Languages language, byte[] localisationDefinitionData)
        {
            if (localisationDefinitionData == null) return;
            try
            {
                string languageString = language.GetString();
                File.WriteAllBytes(GetCachePath(languageString), localisationDefinitionData);
                string versionPath = GetCachePathVersion(languageString);
                string clientVersion = GetClientVersion();
                File.WriteAllBytes(versionPath, Encoding.ASCII.GetBytes(clientVersion));
            }
            catch (Exception) { }
        }

        // 0x0600006a; ARM64 0x1a3d9fc. No catch here; caller's cache-only
        // catch may retry streaming IO. Preserve the original false base gate.
        private static byte[] LoadLocalisationDefinitionFromCache(Languages language)
        {
            VersionChecker.VersionComponent component = VersionChecker.VersionComponent.None;
            if (!Instance.IsDirty()) return null;
            string languageString = language.GetString();
            string versionPath = GetCachePathVersion(languageString);
            if (!File.Exists(versionPath)) return null;
            byte[] cachedVersionBytes = File.ReadAllBytes(versionPath);
            string cachedVersion = Encoding.ASCII.GetString(cachedVersionBytes);
            if (string.IsNullOrEmpty(cachedVersion)) return null;
            if (!VersionChecker.IsVersionUpToDate(cachedVersion, Instance.GetClientVersion(), out component)) return null;
            string dataPath = GetCachePath(languageString);
            if (!File.Exists(dataPath)) return null;
            return File.ReadAllBytes(dataPath);
        }

        // 0x0600006b; ARM64 0x1a3f4ac.
        protected virtual string GetClientVersion() => "0";

        // 0x0600006c; ARM64 0x1a3ed64.
        private void RequestStringTable() => OnRequestStringTable?.Invoke();

        // 0x0600006d; ARM64 0x1a3f4f0. stringId is unused.
        private static string GetMissingString(Strings stringId) => string.Empty;

        // 0x0600006e; ARM64 0x1a3f53c. Genuine MonoSingleton base only.
        public StringTable() { }
    }
}
