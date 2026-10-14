using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight.AutoPlay
{
	// Original HLAutoPlay.Runtime 02000008.
	[Serializable]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ProfilingDataReference
	{
		[SerializeField]
		private string m_groupName;
		[SerializeField]
		private string m_layoutName;
		[SerializeField]
		private string m_displayName;
		[SerializeField]
		private string m_statName;
	}
}
