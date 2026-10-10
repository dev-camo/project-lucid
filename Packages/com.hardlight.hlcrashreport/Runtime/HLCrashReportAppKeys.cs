using UnityEngine;

namespace Hardlight
{
    [CreateAssetMenu(fileName = "HLCrashReportAppKeys",
        menuName = "Hardlight/HLCrashReport/AppCenter Crash Report App Keys", order = 0)]
    public class HLCrashReportAppKeys : ScriptableObject
    {
        [SerializeField] private string m_iOSAppSecretProd;
        [SerializeField] private string m_iOSAppSecretQA;
        [SerializeField] private string m_googleAppSecretProd;
        [SerializeField] private string m_googleAppSecretQA;
        [SerializeField] private string m_amazonAppSecretProd;
        [SerializeField] private string m_amazonAppSecretQA;
        [SerializeField] private string m_samsungAppSecretProd;
        [SerializeField] private string m_samsungAppSecretQA;

        // HLCrashReport.Runtime 0600000b: actual supplied macOS release constant.
        // No platform selection survived in either native slice of this method.
        public string GetCrashReportAppKey() => "dummyAppKey";

        // 0600000c: buildQA is unused in the supplied release; diagnostic precedes return.
        private string GetCrashReportKeyInternal(bool buildQA)
        {
            HLOutput.LogError("Crash reporting app secret is missing for this type of build.", this);
            return string.Empty;
        }

        // 0600000d: no original field initializers beyond CLR defaults.
        public HLCrashReportAppKeys() { }
    }
}
