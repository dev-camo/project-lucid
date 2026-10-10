using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "HLHapticsConfigurationAsset",
        menuName = "Hardlight/HLHaptics/Module Configuration", order = 0)]
    public class HLHapticsConfigurationAsset : SystemConfigurationAsset, IModuleConfigurationWithFileList
    {
        // Original06000006: exact native strings and ordered field initializers
        // occur before the genuine SystemConfigurationAsset constructor.
        private readonly string[] m_requiredNativeFiles = { "VibrationController.h", "VibrationController.mm" };
        [SerializeField] private string[] m_includedNativeFiles = Array.Empty<string>();
        [SerializeField] private string m_nativeCodePath = "/NativeiOS\\~/";

        // Original06000003 is empty in both supplied player architectures.
        // Editor-only validation source before compilation cannot be inferred.
        public override void Validate() { }

        // Original06000004 uses the real Core AddRange helper twice, required
        // files first. Preserve its null/duplicate behavior through that helper.
        public IEnumerable<string> GetIncludedNativeFiles()
        {
            var files = new HashSet<string>();
            files.AddRange(m_requiredNativeFiles);
            files.AddRange(m_includedNativeFiles);
            return files;
        }

        // Original06000005 retains the serialized path without normalization.
        public string GetNativeCodePath() => m_nativeCodePath;
    }
}
