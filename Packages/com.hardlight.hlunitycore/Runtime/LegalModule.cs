using System;
using System.Collections;
using System.IO;
using System.Text;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Networking;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x0200014d; bodies translated from the 1.10.1
    // x86-64 binary (MethodDefs 0x06000947..0x060009ce). Age gate, consent and
    // advertising flow for the registered ILegalPlugin, backed by a cached copy
    // of the packaged/downloaded legal version and country regulation index.
    //
    // Log (0x0600097e) is [Conditional("UNITY_EDITOR")], so the retail player
    // build carries none of its call sites. The constructor still evaluates one
    // orphaned string.IsNullOrEmpty condition whose body was such a call; it is
    // kept with an empty body. Other removed calls left no trace and are not
    // reconstructed.
    //
    // Members keep their original order: Roslyn numbers generated types by
    // member index, nested types included, which fixes <LegalFlow>d__52 through
    // <StartCountryRegulationIndexFileDownload>d__93 and <>c__DisplayClass71_0..78_0.
    // Those ordinals require all nine nested types (0x0200014e..0x02000156, in
    // token order) to precede LegalFlow; declared last they shift every name by
    // 9. Their exact position among the earlier fields is not recoverable, so
    // they lead the class body.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class LegalModule : IDisposable
    {
        // Original 0x0200014e.
        public enum AdType
        {
            None = 0,
            Generic = 1,
            Behavioural = 2,
        }

        // Original 0x0200014f.
        public enum LegalFlowState
        {
            None = 0,
            AgeGate = 1,
            Consent = 2,
            AdImprovement = 3,
            AdImprovementConfirmation = 4,
            AdTypeSelection = 5,
            TrackingAuthorisation = 6,
            GrantAccess = 7,
            Complete = 8,
            Error = 9,
        }

        // Original 0x02000150.
        public enum AgeType
        {
            Under = 0,
            Mid = 1,
            Over = 2,
        }

        // Original 0x02000151.
        public enum TrackingAuthorisationStatus
        {
            None = -1,
            NotDetermined = 0,
            Restricted = 1,
            Denied = 2,
            Authorised = 3,
        }

        // Original 0x02000152; constructor 0x0600097f.
        public class InitialisationData
        {
            public bool m_appAimedAtChildren = true;
            public string m_cacheFileName;
            public string m_versionFileURL;
            public string m_countryRegulationIndexFileURL;
            public string m_packagedVersionJSON;
            public string m_packageCountryRegulationIndexJSON;
            public bool m_appUsesBehaviouralAds;
            public string m_appVersion;
            public string m_isoCountryCode;
        }

        // Original 0x02000153; constructor 0x06000980.
        public class RequestAccessData
        {
            public int m_playerAge;
            public int m_lastConsentedAge;
            public int m_lastConsentedTargetAge;
            public int m_lastConsentedLegalVersion;
            public int m_lastConsentedCountryVersion;
            public int m_lastConsentedAdAge;
            public int m_lastConsentedTargetAdAge;
            public string m_lastConsentedCountryCode = "";
            public string m_lastConsentedAppVersion = "";
            public AdType m_adType;
            public bool m_initialBoot;
        }

        // Original 0x02000154; constructor 0x06000981.
        [Serializable]
        public sealed class LegalModuleCacheAPI
        {
            public VersionAPI m_versionData;
            public CountryRegulationIndexAPI m_countryIndex;
        }

        // Original 0x02000155.
        public enum UpdateState
        {
            CheckingForUpdates = 0,
            UpToDate = 1,
            UpdateFailed = 2,
        }

        // Original 0x02000156.
        public enum LegalDocumentType
        {
            TermsOfUse = 0,
            PrivacyPolicy = 1,
        }

        public const string CountryTypeConsent = "consent";
        public const string CountryTypeCoppa = "coppa";
        public const string CountryTypeCcpa = "ccpa";
        public const string CountryTypeNone = "none";
        public const string DefaultType = "coppa";
        public const int DefaultAge = -1;
        public const int DefaultOverAge = -1;

        private InitialisationData m_initialisationData;
        private LegalModuleCacheAPI m_cacheData;
        private Coroutine m_downloadCoroutine;
        private Coroutine m_legalDocumentCoroutine;
        private Coroutine m_legalFlowCoroutine;
        private LegalFlowState m_legalFlowState;
        private CountryRegulationIndexAPI.Country m_currentCountry;

        // 0x06000974 stores age before id and type.
        private readonly CountryRegulationIndexAPI.Country DefaultCountry = new CountryRegulationIndexAPI.Country
        {
            age = 13,
            id = "default",
            type = DefaultType,
        };

        private ILegalPlugin m_plugin;
        private RequestAccessData m_requestAccessData;
        private SystemRef<CoroutineUtils> m_coroutineUtils;

        private const int LegalUrlsTimeout = 3;
        private const int VersionTimeout = 4;
        private const int CountryTimeout = 5;

        public UpdateState State { get; private set; }

        public event Action<string> OnOutput;

        private bool m_rejectionAllowed;
        private bool m_sendAgeDataToServer;
        private AdType m_previousAdType;
        private bool m_useRestrictedAds;
        private bool m_requireReconsent;
        private TrackingAuthorisationStatus m_trackingAuthorisationStatus = TrackingAuthorisationStatus.None;
        private bool m_improveExperienceMidAge;
        private bool m_improveExperienceOverAge;
        private bool m_improveExperienceExcludedFromBootFlowOverage;

        // 0x0600094b: a null output handler is not subscribed.
        public static LegalModule CreateAndInitialise(InitialisationData data, Action<string> onOutput)
        {
            LegalModule legalModule = new LegalModule(data);
            if (onOutput != null) legalModule.OnOutput += onOutput;
            legalModule.Initialise();
            return legalModule;
        }

        // 0x0600094c.
        public void RegisterPlugin(ILegalPlugin plugin)
        {
            m_plugin = plugin;
        }

        // 0x0600094d.
        public void SetRequestAccessData(RequestAccessData data)
        {
            m_requestAccessData = data;
        }

        // 0x0600094e: a non-empty country code replaces the initialisation code
        // and reselects the country before the common overload runs.
        public void RequestAccess(RequestAccessData data, string isoCountryCode, bool rejectionAllowed = false, bool sendAgeDataToServer = true, bool requireReconsent = false)
        {
            if (m_plugin == null)
                throw new UnityException("No plugin as been registered, call RegisterPlugin before RequestAccess");
            if (!string.IsNullOrEmpty(isoCountryCode))
            {
                m_initialisationData.m_isoCountryCode = isoCountryCode;
                m_currentCountry = FindCountry(m_initialisationData.m_isoCountryCode, m_cacheData.m_countryIndex.countries);
            }
            RequestAccess(data, rejectionAllowed, sendAgeDataToServer, requireReconsent);
        }

        // 0x0600094f: reconsent requests accumulate; a country change only
        // forces one for behavioural-ads apps. The tracking status is sampled
        // before the coroutine host is fetched.
        public void RequestAccess(RequestAccessData data, bool rejectionAllowed = false, bool sendAgeDataToServer = true, bool requireReconsent = false)
        {
            if (m_plugin == null)
                throw new UnityException("No plugin as been registered, call RegisterPlugin before RequestAccess");
            if (SaveDataUpgradeRequired(data))
            {
                data.m_lastConsentedCountryCode = m_initialisationData.m_isoCountryCode;
                data.m_lastConsentedAppVersion = m_initialisationData.m_appVersion;
            }
            m_requestAccessData = data;
            m_rejectionAllowed = rejectionAllowed;
            m_sendAgeDataToServer = sendAgeDataToServer;
            m_requireReconsent |= (m_initialisationData.m_appUsesBehaviouralAds
                && !m_initialisationData.m_isoCountryCode.Equals(m_requestAccessData.m_lastConsentedCountryCode, StringComparison.OrdinalIgnoreCase))
                || requireReconsent;
            m_trackingAuthorisationStatus = (TrackingAuthorisationStatus)HLUnityCore.RequestTrackingAuthorisationStatus();
            SetLegalFlowState(LegalFlowState.None);
            m_legalFlowCoroutine = m_coroutineUtils.Get().StartCoroutine(LegalFlow());
        }

        // 0x06000950: consent saved before the country/app version fields existed.
        private bool SaveDataUpgradeRequired(RequestAccessData data)
        {
            return data.m_lastConsentedLegalVersion != 0
                && !(data.m_lastConsentedCountryCode != string.Empty && data.m_lastConsentedAppVersion != string.Empty);
        }

        // 0x06000951; MoveNext 0x060009b6.
        private IEnumerator LegalFlow()
        {
            while (State == UpdateState.CheckingForUpdates) yield return null;
            SetupCountryData();
            if (m_requireReconsent)
            {
                ResetConsentAccessData();
                ResetAdsAccessData();
                m_plugin.OnDataChange(m_requestAccessData);
                m_requireReconsent = false;
            }
            while (true)
            {
                switch (m_legalFlowState)
                {
                    case LegalFlowState.None:
                        SetLegalFlowState(LegalFlowState.AgeGate);
                        break;
                    case LegalFlowState.AgeGate:
                        yield return CheckForPlayerAge();
                        break;
                    case LegalFlowState.Consent:
                        yield return CheckForConsent();
                        break;
                    case LegalFlowState.AdImprovement:
                        yield return CheckForAdImprovementConsent();
                        break;
                    case LegalFlowState.AdImprovementConfirmation:
                        yield return CheckForAdImprovementConfirmation();
                        break;
                    case LegalFlowState.AdTypeSelection:
                        yield return CheckForAdType();
                        break;
                    case LegalFlowState.TrackingAuthorisation:
                        yield return CheckForTrackingRequestConformation();
                        break;
                    case LegalFlowState.GrantAccess:
                        m_plugin.OnAccessGranted(m_useRestrictedAds);
                        SetLegalFlowState(LegalFlowState.Complete);
                        break;
                    case LegalFlowState.Complete:
                        yield break;
                    case LegalFlowState.Error:
                        throw new Exception("Legal flow errored.");
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        // 0x06000952.
        private void SetLegalFlowState(LegalFlowState flowState)
        {
            m_legalFlowState = flowState;
        }

        // 0x06000953: case-insensitive type match, unlike IsCountryGDPR.
        public bool CanShowOptOutButton()
        {
            return m_currentCountry.type.Equals(CountryTypeConsent, StringComparison.OrdinalIgnoreCase) && m_requestAccessData != null;
        }

        // 0x06000954.
        public bool CanShowOptOutAdsButton()
        {
            return m_requestAccessData != null
                && CountryRequiresAdConsent()
                && m_requestAccessData.m_playerAge >= m_currentCountry.age
                && m_requestAccessData.m_lastConsentedAdAge == m_requestAccessData.m_playerAge;
        }

        // 0x06000955.
        public void OptOut()
        {
            if (m_plugin == null)
                throw new UnityException("No plugin as been registered, call RegisterPlugin before OptOut");
            if (CanShowOptOutButton())
            {
                ResetConsentAccessData();
                ResetAdsAccessData();
                m_plugin.OnDataChange(m_requestAccessData);
                m_plugin.OnAccessRevoked(true);
            }
        }

        // 0x06000956: the flow state is cleared before the ad fields.
        public void OptOutAds()
        {
            if (m_plugin == null)
                throw new UnityException("No plugin as been registered, call RegisterPlugin before OptOutAds");
            if (CanShowOptOutAdsButton())
            {
                SetLegalFlowState(LegalFlowState.None);
                ResetAdsAccessData();
                m_plugin.OnDataChange(m_requestAccessData);
                m_plugin.OnAccessRevoked(true);
            }
        }

        // 0x06000957.
        private void ResetConsentAccessData()
        {
            m_requestAccessData.m_lastConsentedAge = 0;
            m_requestAccessData.m_lastConsentedTargetAge = 0;
            m_requestAccessData.m_lastConsentedLegalVersion = 0;
            m_requestAccessData.m_lastConsentedCountryVersion = 0;
            m_requestAccessData.m_lastConsentedCountryCode = "";
            m_requestAccessData.m_lastConsentedAppVersion = "";
        }

        // 0x06000958: the previous ad type is remembered for CheckForAdType.
        private void ResetAdsAccessData()
        {
            m_previousAdType = m_requestAccessData.m_adType;
            m_requestAccessData.m_lastConsentedAdAge = 0;
            m_requestAccessData.m_lastConsentedTargetAdAge = 0;
            m_requestAccessData.m_adType = AdType.None;
        }

        // 0x06000959.
        public bool CanShowSetAgeButton()
        {
            return CountryRequiresAgeGate();
        }

        // 0x0600095a: the original reuses the RequestAccess message here.
        public void ChangeAge(int age)
        {
            if (m_plugin == null)
                throw new UnityException("No plugin as been registered, call RegisterPlugin before RequestAccess");
            if (m_requestAccessData.m_playerAge == age) return;
            m_requestAccessData.m_playerAge = age;
            bool accessRevoked = false;
            if (CountryRequiresConsent() || CountryRequiresAgeGate())
            {
                ResetConsentAccessData();
                ResetAdsAccessData();
                accessRevoked = true;
            }
            m_plugin.OnDataChange(m_requestAccessData);
            if (accessRevoked) m_plugin.OnAccessRevoked(false);
        }

        // 0x0600095b: the host is fetched before the iterator is created.
        public void RetrieveLegalDocumentURL(LegalDocumentType documentType, Action<bool, string> callback)
        {
            if (m_plugin == null)
                throw new UnityException("No plugin as been registered, call RegisterPlugin before RequestAccess");
            m_legalDocumentCoroutine = m_coroutineUtils.Get().StartCoroutine(RetrieveLegalDocumentURLCoroutine(documentType, callback));
        }

        // 0x0600095c..0x0600095e: exact string ==, unlike the Equals checks.
        public bool IsCountryGDPR()
        {
            return GetCountryConsentType() == CountryTypeConsent;
        }

        public bool IsCountryCCPA()
        {
            return GetCountryConsentType() == CountryTypeCcpa;
        }

        public bool IsCountryCOPPA()
        {
            return GetCountryConsentType() == CountryTypeCoppa;
        }

        // 0x0600095f: no plugin check, and its own "has not been" message.
        public int GetCountryOverage()
        {
            if (m_currentCountry == null)
                throw new UnityException("Country has not been set, call CreateAndInitialise with a valid country.");
            return m_currentCountry.overAge != DefaultOverAge ? m_currentCountry.overAge : m_currentCountry.age;
        }

        // 0x06000960: plugin check first; the original message reads "has no been".
        public string GetCountryConsentType()
        {
            if (m_plugin == null)
                throw new UnityException("No plugin as been registered, call RegisterPlugin before RequestAccess");
            if (m_currentCountry == null)
                throw new UnityException("Country has no been set, call CreateAndInitialise with a valid country.");
            return m_currentCountry.type;
        }

        // 0x06000961; MoveNext 0x060009bc. A request error exits without
        // clearing m_legalDocumentCoroutine; after a response, the clear runs
        // in a finally, even when the catch's callback throws.
        private IEnumerator RetrieveLegalDocumentURLCoroutine(LegalDocumentType documentType, Action<bool, string> callback)
        {
            using (UnityWebRequest webRequest = UnityWebRequest.Get(UrlUtils.GetEscaped(m_plugin.GetLegalDocumentsFileURL())))
            {
                webRequest.timeout = LegalUrlsTimeout;
                yield return webRequest.SendWebRequest();
                if (webRequest.error != null)
                {
                    callback(false, null);
                    yield break;
                }
                try
                {
                    LegalDocumentsAPI legalDocuments = JsonUtility.FromJson<LegalDocumentsAPI>(Encoding.UTF8.GetString(webRequest.downloadHandler.data));
                    if (legalDocuments != null)
                    {
                        switch (documentType)
                        {
                            case LegalDocumentType.TermsOfUse:
                                callback(true, legalDocuments.TOU);
                                break;
                            case LegalDocumentType.PrivacyPolicy:
                                callback(true, legalDocuments.PP);
                                break;
                        }
                    }
                }
                catch (Exception)
                {
                    callback(false, null);
                }
                finally
                {
                    m_legalDocumentCoroutine = null;
                }
            }
        }

        // 0x06000962: case-insensitive id match; a null code skips the search.
        private CountryRegulationIndexAPI.Country FindCountry(string isoCountryCode, CountryRegulationIndexAPI.Country[] countries)
        {
            if (isoCountryCode != null)
            {
                foreach (CountryRegulationIndexAPI.Country country in countries)
                {
                    if (country.id.Equals(isoCountryCode, StringComparison.OrdinalIgnoreCase)) return country;
                }
            }
            return DefaultCountry;
        }

        // 0x06000963.
        private void SetupCountryData()
        {
            m_improveExperienceMidAge = false;
            m_improveExperienceOverAge = false;
            m_improveExperienceExcludedFromBootFlowOverage = false;
            if (m_currentCountry.type.Equals(CountryTypeConsent, StringComparison.OrdinalIgnoreCase))
            {
                m_improveExperienceOverAge = true;
            }
            else if (m_currentCountry.type.Equals(CountryTypeCcpa, StringComparison.OrdinalIgnoreCase))
            {
                m_improveExperienceMidAge = true;
                m_improveExperienceOverAge = true;
                m_improveExperienceExcludedFromBootFlowOverage = true;
            }
        }

        // 0x06000964; closure <>c__DisplayClass71_0, MoveNext 0x060009aa.
        // Only a non-positive age is requested.
        private IEnumerator CheckForPlayerAge()
        {
            bool awaitingAge = true;
            if (CountryRequiresAgeGate() && m_requestAccessData.m_playerAge <= 0)
            {
                m_plugin.RequestPlayerAge(age =>
                {
                    m_requestAccessData.m_playerAge = age;
                    if (m_sendAgeDataToServer) m_plugin.OnDataChange(m_requestAccessData);
                    awaitingAge = false;
                });
            }
            else
            {
                awaitingAge = false;
            }
            while (awaitingAge) yield return null;
            SetLegalFlowState(LegalFlowState.Consent);
        }

        // 0x06000965.
        private bool CountryRequiresAgeGate()
        {
            return m_initialisationData.m_appAimedAtChildren && CountryRequiresConsent();
        }

        // 0x06000966.
        private bool ConsentIsValid(RequestAccessData data)
        {
            return data.m_lastConsentedAge != 0
                && data.m_lastConsentedLegalVersion >= m_cacheData.m_versionData.L
                && data.m_playerAge == data.m_lastConsentedAge;
        }

        // 0x06000967; closure <>c__DisplayClass74_0, MoveNext 0x060009a4.
        // Both callbacks are allocated before the rejection mode is read.
        private IEnumerator CheckForConsent()
        {
            bool awaitingConsent = true;
            if (GetAgeType() == AgeType.Under) m_useRestrictedAds = true;
            if (CountryRequiresConsent() && !ConsentIsValid(m_requestAccessData))
            {
                Action onAccept = () =>
                {
                    m_requestAccessData.m_lastConsentedAge = m_requestAccessData.m_playerAge;
                    m_requestAccessData.m_lastConsentedTargetAge = m_currentCountry.age;
                    m_requestAccessData.m_lastConsentedLegalVersion = m_cacheData.m_versionData.L;
                    m_requestAccessData.m_lastConsentedCountryVersion = m_cacheData.m_versionData.C;
                    m_requestAccessData.m_lastConsentedCountryCode = m_currentCountry.id;
                    m_requestAccessData.m_lastConsentedAppVersion = m_initialisationData.m_appVersion;
                    m_plugin.OnDataChange(m_requestAccessData);
                    awaitingConsent = false;
                };
                Action onReject = () =>
                {
                    m_plugin.OnAccessRevoked(true);
                    SetLegalFlowState(LegalFlowState.Complete);
                    awaitingConsent = false;
                };
                if (m_rejectionAllowed) m_plugin.RequestConsent(m_useRestrictedAds, onAccept, onReject);
                else m_plugin.RequestConsent(m_useRestrictedAds, onAccept);
            }
            else
            {
                awaitingConsent = false;
            }
            while (awaitingConsent) yield return null;
            if (m_legalFlowState != LegalFlowState.Complete)
                SetLegalFlowState(m_initialisationData.m_appUsesBehaviouralAds ? LegalFlowState.AdImprovement : LegalFlowState.GrantAccess);
        }

        // 0x06000968: the ad type is read from m_requestAccessData, not data.
        private bool AdConsentIsValid(RequestAccessData data)
        {
            return data.m_lastConsentedAdAge != 0
                && data.m_playerAge == data.m_lastConsentedAdAge
                && m_requestAccessData.m_adType != AdType.None;
        }

        // 0x06000969; closure <>c__DisplayClass76_0, MoveNext 0x06000998.
        // Without a prompt, an unset ad type is chosen from the age class and
        // only the behavioural choice records the consented age.
        private IEnumerator CheckForAdImprovementConsent()
        {
            LegalFlowState legalFlowState;
            bool awaitingConsent;
            IAdLegalPlugin plugin = m_plugin as IAdLegalPlugin;
            if (plugin != null)
            {
                awaitingConsent = true;
                legalFlowState = LegalFlowState.AdTypeSelection;
                if (!ImproveExperienceExcluded() && RequiresImproveExperiencePrompt() && !AdConsentIsValid(m_requestAccessData))
                {
                    plugin.RequestAdImprovementConsent(() =>
                    {
                        if (RequiresImproveExperienceConfirmation())
                        {
                            legalFlowState = LegalFlowState.AdImprovementConfirmation;
                        }
                        else
                        {
                            m_requestAccessData.m_adType = AdType.Behavioural;
                            m_requestAccessData.m_lastConsentedAdAge = m_requestAccessData.m_playerAge;
                            m_requestAccessData.m_lastConsentedTargetAdAge = m_currentCountry.age;
                        }
                        m_requestAccessData.m_lastConsentedCountryVersion = m_cacheData.m_versionData.C;
                        m_plugin.OnDataChange(m_requestAccessData);
                        awaitingConsent = false;
                    }, () =>
                    {
                        awaitingConsent = false;
                    });
                }
                else
                {
                    awaitingConsent = false;
                    if (m_requestAccessData.m_adType == AdType.None)
                    {
                        if (GetAgeType() == AgeType.Under)
                        {
                            m_requestAccessData.m_adType = AdType.Generic;
                        }
                        else
                        {
                            m_requestAccessData.m_adType = AdType.Behavioural;
                            m_requestAccessData.m_lastConsentedAdAge = m_requestAccessData.m_playerAge;
                        }
                        m_plugin.OnDataChange(m_requestAccessData);
                    }
                }
                while (awaitingConsent) yield return null;
                SetLegalFlowState(legalFlowState);
            }
            else
            {
                SetLegalFlowState(LegalFlowState.Error);
            }
        }

        // 0x0600096a; closure <>c__DisplayClass77_0, MoveNext 0x06000992.
        private IEnumerator CheckForAdImprovementConfirmation()
        {
            bool awaitingConsent;
            LegalFlowState legalFlowState;
            IAdLegalPlugin plugin = m_plugin as IAdLegalPlugin;
            if (plugin != null)
            {
                awaitingConsent = true;
                legalFlowState = LegalFlowState.AdTypeSelection;
                if (!ImproveExperienceExcluded() && RequiresImproveExperienceConfirmation() && !AdConsentIsValid(m_requestAccessData))
                {
                    plugin.RequestAdImprovementConfirmation(() =>
                    {
                        m_requestAccessData.m_lastConsentedAdAge = m_requestAccessData.m_playerAge;
                        m_requestAccessData.m_lastConsentedTargetAdAge = m_currentCountry.age;
                        m_requestAccessData.m_adType = AdType.Behavioural;
                        m_plugin.OnDataChange(m_requestAccessData);
                        awaitingConsent = false;
                    }, () =>
                    {
                        awaitingConsent = false;
                        legalFlowState = LegalFlowState.AdImprovement;
                    });
                }
                else
                {
                    awaitingConsent = false;
                }
                while (awaitingConsent) yield return null;
                SetLegalFlowState(legalFlowState);
            }
            else
            {
                SetLegalFlowState(LegalFlowState.Error);
            }
        }

        // 0x0600096b; closure <>c__DisplayClass78_0, MoveNext 0x0600099e.
        private IEnumerator CheckForAdType()
        {
            IAdLegalPlugin plugin = m_plugin as IAdLegalPlugin;
            bool awaitingAdTypeSelection;
            LegalFlowState legalFlowState;
            if (plugin != null)
            {
                awaitingAdTypeSelection = true;
                legalFlowState = LegalFlowState.TrackingAuthorisation;
                if (!ImproveExperienceExcluded() && RequiresImproveExperiencePrompt() && !AdConsentIsValid(m_requestAccessData))
                {
                    plugin.RequestAdType(adType =>
                    {
                        m_requestAccessData.m_lastConsentedAdAge = m_requestAccessData.m_playerAge;
                        m_requestAccessData.m_lastConsentedTargetAdAge = m_currentCountry.age;
                        m_requestAccessData.m_adType = adType;
                        m_plugin.OnDataChange(m_requestAccessData);
                        m_useRestrictedAds = adType == AdType.Generic;
                        // Only a change from a previously recorded type is announced.
                        if (m_previousAdType != AdType.None && m_previousAdType != m_requestAccessData.m_adType)
                            plugin.OnAdTypeSelectionChanged(m_requestAccessData.m_adType);
                        awaitingAdTypeSelection = false;
                    }, () =>
                    {
                        legalFlowState = LegalFlowState.AdImprovement;
                        awaitingAdTypeSelection = false;
                    });
                }
                else
                {
                    awaitingAdTypeSelection = false;
                    m_useRestrictedAds = m_requestAccessData.m_adType == AdType.Generic;
                }
                while (awaitingAdTypeSelection) yield return null;
                SetLegalFlowState(legalFlowState);
            }
            else
            {
                SetLegalFlowState(LegalFlowState.Error);
            }
        }

        // 0x0600096c; MoveNext 0x060009b0. Only an undetermined status prompts;
        // unknown statuses are reported and still grant access.
        private IEnumerator CheckForTrackingRequestConformation()
        {
            if (!m_useRestrictedAds && HLUnityCore.CanDeviceRequestTrackingAuthorisation())
            {
                switch (m_trackingAuthorisationStatus)
                {
                    case TrackingAuthorisationStatus.None:
                    case TrackingAuthorisationStatus.Restricted:
                    case TrackingAuthorisationStatus.Denied:
                    case TrackingAuthorisationStatus.Authorised:
                        break;
                    case TrackingAuthorisationStatus.NotDetermined:
                        HLUnityCore unityCore = ProcessManager.GetSystem<HLUnityCore>();
                        unityCore.AddTrackingAuthorisationHandler(TrackingAuthorisationCallback);
                        HLUnityCore.RequestTrackingAuthorisation();
                        while (m_trackingAuthorisationStatus == TrackingAuthorisationStatus.NotDetermined) yield return null;
                        unityCore.RemoveTrackingAuthorisationHandler(TrackingAuthorisationCallback);
                        break;
                    default:
                        HLOutput.LogError(string.Format("Legal Module - Unsupported tracking status: {0}", m_trackingAuthorisationStatus));
                        break;
                }
            }
            SetLegalFlowState(LegalFlowState.GrantAccess);
        }

        // 0x0600096d.
        private void TrackingAuthorisationCallback(int trackingStatus)
        {
            m_trackingAuthorisationStatus = (TrackingAuthorisationStatus)trackingStatus;
        }

        // 0x0600096e.
        private bool CountryRequiresConsent()
        {
            return IsCountryGDPR() || IsCountryCCPA() || IsCountryCOPPA();
        }

        // 0x0600096f.
        private bool CountryRequiresAdConsent()
        {
            return !m_currentCountry.type.Equals(CountryTypeCoppa, StringComparison.OrdinalIgnoreCase);
        }

        // 0x06000970: every non-mid age class reads the over-age flag.
        private bool RequiresImproveExperiencePrompt()
        {
            return GetAgeType() == AgeType.Mid ? m_improveExperienceMidAge : m_improveExperienceOverAge;
        }

        // 0x06000971.
        private bool ImproveExperienceExcluded()
        {
            AgeType ageType = GetAgeType();
            return ageType == AgeType.Under
                || (ageType == AgeType.Over && m_requestAccessData.m_initialBoot && m_improveExperienceExcludedFromBootFlowOverage);
        }

        // 0x06000972: mid-age players confirm unless tracking is still undetermined.
        private bool RequiresImproveExperienceConfirmation()
        {
            if (GetAgeType() != AgeType.Mid) return false;
            if (!HLUnityCore.CanDeviceRequestTrackingAuthorisation()) return true;
            return (TrackingAuthorisationStatus)HLUnityCore.RequestTrackingAuthorisationStatus() != TrackingAuthorisationStatus.NotDetermined;
        }

        // 0x06000973: without an over-age, an unset age counts as over age.
        public AgeType GetAgeType()
        {
            int playerAge = m_requestAccessData.m_playerAge;
            if (m_currentCountry.overAge != DefaultOverAge)
            {
                if (playerAge >= m_currentCountry.overAge) return AgeType.Over;
                return playerAge >= m_currentCountry.age ? AgeType.Mid : AgeType.Under;
            }
            if (playerAge != DefaultAge && playerAge < m_currentCountry.age) return AgeType.Under;
            return AgeType.Over;
        }

        // 0x06000974: all five inputs are validated in this order before the
        // CoroutineUtils reference is requested.
        protected LegalModule(InitialisationData data)
        {
            m_initialisationData = data;
            SetState(UpdateState.CheckingForUpdates);
            if (string.IsNullOrEmpty(m_initialisationData.m_countryRegulationIndexFileURL))
                throw new ArgumentException("m_countryRegulationIndexFileURL is null or empty.");
            if (string.IsNullOrEmpty(m_initialisationData.m_packageCountryRegulationIndexJSON))
                throw new ArgumentException("m_packageCountryRegulationIndexJSON is null or empty.");
            if (string.IsNullOrEmpty(m_initialisationData.m_packagedVersionJSON))
                throw new ArgumentException("m_packagedVersionJSON is null or empty.");
            if (string.IsNullOrEmpty(m_initialisationData.m_versionFileURL))
                throw new ArgumentException("m_versionFileURL is null or empty.");
            if (string.IsNullOrEmpty(m_initialisationData.m_cacheFileName))
                throw new ArgumentException("m_cacheFilePath is null or empty.");
            if (string.IsNullOrEmpty(m_initialisationData.m_isoCountryCode))
            {
            }
            m_coroutineUtils = ProcessManager.GetSystemRef<CoroutineUtils>();
        }

        // 0x06000975.
        public void Dispose()
        {
            OnOutput = null;
            SetLegalFlowState(LegalFlowState.None);
            if (m_downloadCoroutine != null)
            {
                m_coroutineUtils.Get().StopCoroutine(m_downloadCoroutine);
                m_downloadCoroutine = null;
            }
            if (m_legalDocumentCoroutine != null)
            {
                m_coroutineUtils.Get().StopCoroutine(m_legalDocumentCoroutine);
                m_legalDocumentCoroutine = null;
            }
            if (m_legalFlowCoroutine != null)
            {
                m_coroutineUtils.Get().StopCoroutine(m_legalFlowCoroutine);
                m_legalFlowCoroutine = null;
            }
        }

        // 0x06000976.
        private string GetSaveFilePath()
        {
            return string.Format("{0}/{1}", Application.persistentDataPath, m_initialisationData.m_cacheFileName);
        }

        // 0x06000977: file faults are swallowed and keep the cache.
        public void ClearCache()
        {
            try
            {
                if (File.Exists(GetSaveFilePath()))
                {
                    File.Delete(GetSaveFilePath());
                    m_cacheData = null;
                }
            }
            catch (Exception)
            {
            }
        }

        // 0x06000978: a readable cache wins over the packaged JSON; the packaged
        // fallback is saved immediately. Only the cache read is guarded.
        private void Initialise()
        {
            try
            {
                if (File.Exists(GetSaveFilePath()))
                {
                    string json = File.ReadAllText(GetSaveFilePath(), Encoding.UTF8);
                    if (json != null) m_cacheData = JsonUtility.FromJson<LegalModuleCacheAPI>(json);
                }
            }
            catch (Exception)
            {
            }
            if (m_cacheData == null)
            {
                m_cacheData = new LegalModuleCacheAPI();
                m_cacheData.m_versionData = JsonUtility.FromJson<VersionAPI>(m_initialisationData.m_packagedVersionJSON);
                if (m_cacheData.m_versionData == null)
                {
                    SetState(UpdateState.UpdateFailed);
                    throw new FormatException("String passed into m_packagedVersionJSON must be valid JSON");
                }
                m_cacheData.m_countryIndex = JsonUtility.FromJson<CountryRegulationIndexAPI>(m_initialisationData.m_packageCountryRegulationIndexJSON);
                if (m_cacheData.m_countryIndex == null || m_cacheData.m_countryIndex.countries == null)
                {
                    SetState(UpdateState.UpdateFailed);
                    throw new FormatException("String passed into m_packageCountryRegulationIndexJSON must be valid JSON");
                }
                SaveCache();
            }
            m_currentCountry = FindCountry(m_initialisationData.m_isoCountryCode, m_cacheData.m_countryIndex.countries);
            m_downloadCoroutine = m_coroutineUtils.Get().StartCoroutine(StartVersionFileDownload());
        }

        // 0x06000979; MoveNext 0x060009ca. Only the parse is guarded. Failures
        // clear m_downloadCoroutine; success leaves it set. A new legal version
        // forces reconsent; a new country version chains the index download.
        private IEnumerator StartVersionFileDownload()
        {
            using (UnityWebRequest webRequest = UnityWebRequest.Get(UrlUtils.GetEscaped(m_initialisationData.m_versionFileURL)))
            {
                webRequest.timeout = VersionTimeout;
                yield return webRequest.SendWebRequest();
                if (webRequest.error != null)
                {
                    SetState(UpdateState.UpdateFailed);
                    m_downloadCoroutine = null;
                    yield break;
                }
                VersionAPI version = null;
                try
                {
                    version = JsonUtility.FromJson<VersionAPI>(Encoding.UTF8.GetString(webRequest.downloadHandler.data));
                }
                catch (Exception)
                {
                }
                if (version == null)
                {
                    SetState(UpdateState.UpdateFailed);
                    m_downloadCoroutine = null;
                    yield break;
                }
                if (m_cacheData.m_versionData.L != version.L)
                {
                    m_cacheData.m_versionData.L = version.L;
                    m_requireReconsent = true;
                    SaveCache();
                }
                if (m_cacheData.m_versionData.C != version.C)
                {
                    // Drained inline (hoisted <nested>5__3 in d__92), not
                    // yielded as a child coroutine.
                    IEnumerator nested = StartCountryRegulationIndexFileDownload(version.C);
                    while (nested.MoveNext()) yield return nested.Current;
                }
                else
                {
                    SetState(UpdateState.UpToDate);
                }
            }
        }

        // 0x0600097a; MoveNext 0x060009c3. Only the parse is guarded.
        private IEnumerator StartCountryRegulationIndexFileDownload(int clientRegulationIndexVersion)
        {
            using (UnityWebRequest webRequest = UnityWebRequest.Get(UrlUtils.GetEscaped(m_initialisationData.m_countryRegulationIndexFileURL)))
            {
                webRequest.timeout = CountryTimeout;
                yield return webRequest.SendWebRequest();
                if (webRequest.error != null)
                {
                    SetState(UpdateState.UpdateFailed);
                    yield break;
                }
                CountryRegulationIndexAPI countryIndex = null;
                try
                {
                    countryIndex = JsonUtility.FromJson<CountryRegulationIndexAPI>(Encoding.UTF8.GetString(webRequest.downloadHandler.data));
                }
                catch (Exception)
                {
                }
                if (countryIndex == null)
                {
                    SetState(UpdateState.UpdateFailed);
                    yield break;
                }
                bool countryUpdated = CurrentCountryHasReceivedUpdate(countryIndex);
                m_requireReconsent = countryUpdated || m_requireReconsent;
                m_cacheData.m_versionData.C = clientRegulationIndexVersion;
                m_cacheData.m_countryIndex = countryIndex;
                SaveCache();
                if (countryUpdated)
                    m_currentCountry = FindCountry(m_initialisationData.m_isoCountryCode, m_cacheData.m_countryIndex.countries);
                SetState(UpdateState.UpToDate);
            }
        }

        // 0x0600097b: compares by Country value equality.
        private bool CurrentCountryHasReceivedUpdate(CountryRegulationIndexAPI newCountryIndex)
        {
            if (m_currentCountry == null) return true;
            return FindCountry(m_initialisationData.m_isoCountryCode, newCountryIndex.countries) != m_currentCountry;
        }

        // 0x0600097c: serialise before the path is built.
        private void SaveCache()
        {
            string json = JsonUtility.ToJson(m_cacheData);
            File.WriteAllText(GetSaveFilePath(), json, Encoding.UTF8);
        }

        // 0x0600097d.
        private void SetState(UpdateState newState)
        {
            if (State != newState) State = newState;
        }

        // 0x0600097e: editor-only; see the type comment.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void Log(string message)
        {
            OnOutput?.Invoke("[Legal] " + message);
        }
    }
}
