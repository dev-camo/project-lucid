using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// HLUnityCore.Runtime TypeDefIndex 8808, methods 0x06000183..0x06000184.
	// Loaded from Resources by ConditionalOutput.InitialiseSettings.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ConditionalOutputConfiguration : SingleScriptableObject
	{
		public const string FileName = "ConditionalOutputConfiguration";

		[SerializeField]
		private ConditionalOutputSettings m_conditionalOutputSettings;

		public ConditionalOutputSettings ConditionalOutputSettings => m_conditionalOutputSettings;
	}
}
