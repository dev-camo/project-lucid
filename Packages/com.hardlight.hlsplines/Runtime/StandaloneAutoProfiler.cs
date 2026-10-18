using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace Hardlight
{
	// Original HLSplines.Runtime 02000078; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Serializable]
	public class StandaloneAutoProfiler : MonoBehaviour
	{
		private SurfaceSetupProfiler m_surfaceSetupProfiler = new SurfaceSetupProfiler();

		// 06000317 (1.10.1 RVA 0x1A767A0): development players only; inactive surfaces are included.
		// The native tail call leaves the receiver register unset (the callee never reads `this`),
		// so m_surfaceSetupProfiler as the second receiver is inferred.
		private void Start()
		{
			if (Debug.isDebugBuild)
				m_surfaceSetupProfiler.CheckForHiddenAndInactiveSurfaces(m_surfaceSetupProfiler.FindObjectsOfTypeInActiveScene<ISurface>(true));
		}

		// 06000318 (1.10.1 RVA 0x1A76850)
		public StandaloneAutoProfiler()
		{
		}
	}
}
