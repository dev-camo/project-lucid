using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLUnityCore.Runtime 0x02000176 (TypeDefIndex 9125), methods
	// 0x06000a65..0x06000a68; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[CreateAssetMenu(fileName = "LoggerSettings", menuName = "Hardlight/LogHandler")]
	public class HLLogHandlerSettingScriptableObject : ScriptableObject
	{
		[SerializeField]
		private HLLogHandlerSetting[] m_platformSettings;

		[SerializeField]
		private ExclusionList[] m_logExclusionList;

		public ExclusionList[] LogExclusionList => m_logExclusionList;

		// 0x06000a66 (0x1b00d80). The first row whose platform matches the active
		// identifier ignoring case; the identifier is read again for every row.
		public HLLogHandlerSetting GetPlatformSetting()
		{
			int count = m_platformSettings.Length;
			for (int i = 0; i < count; i++)
			{
				if (string.Equals(PlatformUtils.GetActivePlatformIdentifier(), m_platformSettings[i].Platform, StringComparison.OrdinalIgnoreCase))
					return m_platformSettings[i];
			}
			return null;
		}

		// 0x06000a67 (0x1b02370) still compares every later row's platform with
		// each row's, but the shipped body discards the result: whatever the
		// duplicate branch did was compiled out of this build.
		private void OnValidate()
		{
			int count = m_platformSettings.Length;
			for (int i = 0; i < count; i++)
			{
				for (int j = i + 1; j < count; j++)
				{
					if (m_platformSettings[i].Platform == m_platformSettings[j].Platform)
					{
					}
				}
			}
		}
	}
}
