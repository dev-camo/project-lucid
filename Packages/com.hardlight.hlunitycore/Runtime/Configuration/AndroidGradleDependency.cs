using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace Hardlight.Utils
{
	// Original HLUnityCore.Runtime 020002a8; bodies translated from the 1.10.1 x86-64 binary.
	[Serializable]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class AndroidGradleDependency : IValidatable
	{
		[SerializeField]
		private string m_name;

		[SerializeField]
		private Version m_version;

		private const string FormatString = "{0}:{1}";

		private const char FormatSeparator = ':';

		public string Name
		{
			get
			{
				return m_name;
			}
		}

		public string Version
		{
			get
			{
				return m_version.ToString();
			}
		}

		// 06001146 (1.10.1 RVA 0x1B30400)
		public AndroidGradleDependency(string dependency)
		{
			if (string.IsNullOrEmpty(dependency))
			{
				m_name = null;
				m_version = default;
			}
			else
			{
				int separatorIndex = dependency.LastIndexOf(FormatSeparator);
				m_name = dependency.Substring(0, separatorIndex);
				string versionString = dependency.Substring(separatorIndex + 1, dependency.Length - (separatorIndex + 1));
				m_version = new Version(versionString);
				Validate();
			}
		}

		// 06001147 (1.10.1 RVA 0x1B30560)
		public AndroidGradleDependency(string name, Version version)
		{
			m_name = name;
			m_version = version;
			Validate();
		}

		// 06001148 (1.10.1 RVA 0x1B30650)
		public AndroidGradleDependency(string name, string versionNumber)
			: this(name, new Version(versionNumber))
		{
		}

		// 06001149 (1.10.1 RVA 0x1B307B0)
		public override string ToString()
		{
			return string.Format(FormatString, m_name, m_version);
		}

		// 0600114a (1.10.1 RVA 0x1B30730)
		public virtual void Validate()
		{
		}
	}
}
