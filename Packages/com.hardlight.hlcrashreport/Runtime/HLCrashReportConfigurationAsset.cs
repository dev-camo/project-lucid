using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight
{
    [CreateAssetMenu(fileName = "HLCrashReportConfigurationAsset",
        menuName = "Hardlight/HLCrashReport/Module Configuration", order = 0)]
    public class HLCrashReportConfigurationAsset : SystemConfigurationAsset, IModuleConfigurationWithFileList
    {
        internal enum SupportedCrashReportService { AppCenter = 0, Crashlytics = 1, None = 2 }
        private readonly string[] m_requiredNativeFiles = { "HLCrashReport.h", "HLCrashReport.mm" };
        [SerializeField] private string[] m_includedNativeFiles = Array.Empty<string>();
        [SerializeField] private string m_nativeCodePath = "/NativeiOS\\~/";
        [SerializeField] private SupportedCrashReportService m_crashReportService;
        [SerializeField] private HLCrashReportAppKeys m_appCenterConfiguration;
        [SerializeField] private CrashlyticsConfiguration m_crashlyticsConfiguration;
        [SerializeField] private bool m_useErrorExclusion;
        [ShowIf("m_useErrorExclusion", null)]
        [SerializeField] private ExclusionList m_errorExclusionList = new ExclusionList();

        // HLCrashReport.Runtime 06000031..35: preserve public/internal visibility.
        public bool UseErrorExclusion => m_useErrorExclusion;
        public ExclusionList ErrorExclusionList => m_errorExclusionList;
        internal SupportedCrashReportService CrashReportService => m_crashReportService;
        internal HLCrashReportAppKeys AppCenterConfiguration => m_appCenterConfiguration;
        internal CrashlyticsConfiguration CrashlyticsConfiguration => m_crashlyticsConfiguration;

        // 06000036: the original list receives the flag, with no null guard.
        public void Initialise() => m_errorExclusionList.Enabled = m_useErrorExclusion;
        // 06000037: supplied release genuinely contains only RET.
        public override void Validate() { }

        // 06000038: AddRange is the actual Hardlight collection extension. Its
        // null-range diagnostic and enumeration/disposal behavior remain intact.
        public IEnumerable<string> GetIncludedNativeFiles()
        {
            HashSet<string> files = new HashSet<string>();
            files.AddRange(m_requiredNativeFiles);
            files.AddRange(m_includedNativeFiles);
            return files;
        }

        // 06000039: both native jump tables map 0/1/2 to these exact suffixes.
        public string GetNativeCodePath()
        {
            switch (m_crashReportService)
            {
                case SupportedCrashReportService.AppCenter: return m_nativeCodePath + "/AppCenter/";
                case SupportedCrashReportService.Crashlytics: return m_nativeCodePath + "/Crashlytics/";
                case SupportedCrashReportService.None: return m_nativeCodePath + "/Stub/";
                default: throw new InvalidOperationException("Unsupported CrashReportService");
            }
        }

        // 0600003a: all authored initializer allocations precede the genuine base.
        public HLCrashReportConfigurationAsset() { }
    }
}
