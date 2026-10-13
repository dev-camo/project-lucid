using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hardlight
{
	// Original HLSplines.Runtime 02000077; bodies translated from the 1.10.1 x86-64 binary.
	[Serializable]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class SurfaceSetupProfiler
	{
		// 06000314 (1.10.1 RVA 0x1A762D0)
		public void CheckForHiddenAndInactiveSurfaces(List<ISurface> surfaces, bool logSurfaceName = false)
		{
			int hiddenCount = 0;
			int inactiveCount = 0;
			foreach (ISurface surface in surfaces)
			{
				MonoBehaviour monoBehaviour = surface as MonoBehaviour;
				if (monoBehaviour == null)
				{
					continue;
				}
				if ((monoBehaviour.gameObject.hideFlags & HideFlags.HideInHierarchy) != 0)
				{
					if (logSurfaceName)
					{
						Debug.LogWarning("[SurfaceSetupProfiler] '" + monoBehaviour.gameObject.name + "' ISurface is hidden.");
					}
					hiddenCount++;
				}
				else if (!monoBehaviour.isActiveAndEnabled)
				{
					if (logSurfaceName)
					{
						Debug.LogWarning("[SurfaceSetupProfiler] '" + monoBehaviour.gameObject.name + "' ISurface is inactive.");
					}
					inactiveCount++;
				}
			}
			if (hiddenCount > 0)
			{
				Debug.LogWarning(string.Format("[SurfaceSetupProfiler] Total hidden ISurface: {0}.", hiddenCount));
			}
			if (inactiveCount > 0)
			{
				Debug.LogWarning(string.Format("[SurfaceSetupProfiler] Total inactive ISurface: {0}.", inactiveCount));
			}
		}

		// 06000315 (1.10.1 RVA 0x9D0830)
		public List<T> FindObjectsOfTypeInActiveScene<T>(bool includeInactive = false)
		{
			GameObject[] rootGameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
			List<T> result = new List<T>();
			foreach (GameObject rootGameObject in rootGameObjects)
			{
				T[] components = rootGameObject.GetComponentsInChildren<T>(true);
				if (includeInactive)
				{
					result.AddRange(components);
					continue;
				}
				foreach (T component in components)
				{
					if (!(component is MonoBehaviour monoBehaviour) || monoBehaviour.isActiveAndEnabled)
					{
						result.Add(component);
					}
				}
			}
			return result;
		}

		// 06000316 (1.10.1 RVA 0x1A76790)
		public SurfaceSetupProfiler()
		{
		}
	}
}
