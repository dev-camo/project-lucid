using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	[CreateAssetMenu(fileName = "MacOSConfigurationAsset", menuName = "Hardlight/HLUnityCore/MacOSConfigurationAsset", order = 0)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class MacOSConfigurationAsset : SystemConfigurationAsset, IModuleConfigurationWithFileList
	{
		private readonly string[] m_requiredNativeFiles = new[] { "CoreUtilities.h", "CoreUtilities.mm", "HLMacCore.h", "HLMacCore.mm", "HLMacCore/HLOutput.h", "HLMacCore/HLOutput.m" };

		[SerializeField]
		private string[] m_includedNativeFiles = Array.Empty<string>();

		[SerializeField]
		private string m_nativeCodePath = @"/NativeMacOS\~/HLMacCore/";

		// 0x06000164; original ARM64 0x1aa4054 is RET.
        public override void Validate()
		{
		}

		public IEnumerable<string> GetIncludedNativeFiles()
        {
            var result = new HashSet<string>();
            result.AddRange(m_requiredNativeFiles);
            result.AddRange(m_includedNativeFiles);
            return result;
        }

		public string GetNativeCodePath() => m_nativeCodePath;
	}
}
