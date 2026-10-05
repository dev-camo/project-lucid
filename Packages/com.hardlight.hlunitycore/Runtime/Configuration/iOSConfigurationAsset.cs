using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[CreateAssetMenu(fileName = "iOSConfigurationAsset", menuName = "Hardlight/HLUnityCore/iOSConfigurationAsset", order = 0)]
	public class iOSConfigurationAsset : SystemConfigurationAsset, IModuleConfigurationWithFileList
	{
		private readonly string[] m_requiredNativeFiles = new[] { "HLUnityCore.h", "HLUnityCore.mm", "HLOutput.h", "HLOutput.m", "CoreUtilities.h", "CoreUtilities.mm", "IOSAntipiracy.h", "IOSAntipiracy_stubbed.cpp", "ApplicationCore.h", "ApplicationCore.mm", "ApplicationModuleBase.h", "ApplicationModuleBase.mm", "HLHealthMonitor.h", "HLHealthMonitor.mm" };

		private readonly string[] m_requiredFrameworks = new[] { "GameKit.framework", "StoreKit.framework" };

		[SerializeField]
		private string[] m_includedNativeFiles = Array.Empty<string>();

		[SerializeField]
		private string[] m_includedFrameworks = Array.Empty<string>();

		[InspectorName("Main Target OTHER_LDFLAGS")]
		[SerializeField]
		private string[] m_mainTargetOtherLdflags = Array.Empty<string>();

		[SerializeField]
		private string m_nativeCodePath = @"/NativeiOS\~/";

		public IEnumerable<string> IncludedNativeFiles
        {
            get
            {
                var result = new HashSet<string>();
                result.AddRange(m_requiredNativeFiles);
                result.AddRange(m_includedNativeFiles);
                return result;
            }
        }

		public IEnumerable<string> IncludedFrameworks
        {
            get
            {
                var result = new HashSet<string>();
                result.AddRange(m_requiredFrameworks);
                result.AddRange(m_includedFrameworks);
                return result;
            }
        }

		public IEnumerable<string> MainTargetOtherLdflags => m_mainTargetOtherLdflags;

		public IEnumerable<string> GetIncludedNativeFiles() => IncludedNativeFiles;

		public string GetNativeCodePath() => m_nativeCodePath;

		// 0x06000162; original ARM64 0x1aa3b8c is RET.
        public override void Validate()
		{
		}
	}
}
