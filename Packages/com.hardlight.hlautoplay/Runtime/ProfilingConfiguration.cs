using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight.AutoPlay
{
	// Original HLAutoPlay.Runtime 02000007.
	[Serializable]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ProfilingConfiguration
	{
		[SerializeField]
		private HLProfilerDataManager m_profilerDataManager;
		[SerializeField]
		private ProfilingDataReference m_systemUsedMemory;
	}
}
