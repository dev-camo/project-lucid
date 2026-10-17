using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// HLUnityCore.Runtime TypeDefIndex 8784 (0x02000021), methods 0x060000d2..0x060000d5;
	// bodies from the 1.10.1 x86-64 binary (0x1a9ea20..0x1a9eb80). Key/value build
	// information read from a Resources text asset. The shipped data has no
	// Resources/BuildMetaData/BuildMetaData asset, so in the game m_data stays null.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class BuildMetaData
	{
		private Dictionary<string, string> m_data;

		// 0x060000d2.
		public static BuildMetaData Create()
		{
			BuildMetaData buildMetaData = new BuildMetaData();
			buildMetaData.Initialise();
			return buildMetaData;
		}

		// 0x060000d3, 0x1a9ea80: parsed with the default KeyValueParser settings, then the
		// text asset is unloaded.
		private void Initialise()
		{
			TextAsset textAsset = Resources.Load<TextAsset>("BuildMetaData/BuildMetaData");
			if (textAsset == null)
			{
				return;
			}
			m_data = new KeyValueParser().Parse(textAsset.text);
			Resources.UnloadAsset(textAsset);
		}

		// 0x060000d4: null when nothing was loaded or the key is missing.
		public string GetValue(string key)
		{
			return m_data?.TryGetWithDefault(key);
		}
	}
}
