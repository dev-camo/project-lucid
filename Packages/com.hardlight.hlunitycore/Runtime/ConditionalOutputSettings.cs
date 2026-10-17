using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// HLUnityCore.Runtime TypeDefIndex 8814, methods 0x0600018a..0x0600018e. The
	// member order keeps the original <>c__DisplayClass9_0 and 10_0 ordinals.
	[Serializable]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ConditionalOutputSettings
	{
		public enum UnhandledLogBehaviour
		{
			ForceLog = 0,
			Queue = 1
		}

		// TypeDefIndex 8811, .ctor 0x0600018f.
		[Serializable]
		public class LogStateOverride
		{
			public string TypeName;

			public bool Enabled;
		}

		// Both constructors (0x1aa2640, 0x1aa2580) run these initialisers first.
		[SerializeField]
		private bool m_defaultLogState = true;

		[SerializeField]
		private UnhandledLogBehaviour m_preInitialiseLogBehaviour;

		[SerializeField]
		private List<LogStateOverride> m_defaultLogStateOverrides = new List<LogStateOverride>();

		public UnhandledLogBehaviour PreInitialiseLogBehaviour => m_preInitialiseLogBehaviour;

		public ConditionalOutputSettings()
		{
		}

		public ConditionalOutputSettings(bool defaultLogState, UnhandledLogBehaviour preInitialiseLogBehaviour, List<LogStateOverride> defaultLogStateOverrides)
		{
			m_defaultLogState = defaultLogState;
			m_preInitialiseLogBehaviour = preInitialiseLogBehaviour;
			m_defaultLogStateOverrides = defaultLogStateOverrides;
		}

		// 0x0600018d, 0x1aa1a40: overrides match the type's full name exactly.
		public bool GetDefaultLogState(Type type)
		{
			return m_defaultLogStateOverrides.TryFind(entry => entry.TypeName == type.FullName, out LogStateOverride logStateOverride)
				? logStateOverride.Enabled
				: m_defaultLogState;
		}

		// 0x0600018e, 0x1aa2240: update the first match or append a new override.
		public void SetDefaultLogState(Type type, bool value)
		{
			if (m_defaultLogStateOverrides.TryFind(entry => entry.TypeName == type.FullName, out LogStateOverride logStateOverride))
			{
				logStateOverride.Enabled = value;
				return;
			}
			m_defaultLogStateOverrides.Add(new LogStateOverride { TypeName = type.FullName, Enabled = value });
		}
	}
}
