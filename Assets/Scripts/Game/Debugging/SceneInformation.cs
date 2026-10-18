using System.Collections.Generic;
using System.Text;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HardlightProject
{
	// Original Game.Runtime 020001cf; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class SceneInformation
	{
		// 060009ba/060009bb (1.10.1 RVA 0x6F26F0/0x6F2700)
		public string SceneName { get; }
		public int GameObjectCounter { get; }
		public List<StringBuilder> Snapshots = new List<StringBuilder>();
		private SystemRef<HLProfilerDataManager> m_profilerManager = ProcessManager.GetSystemRef<HLProfilerDataManager>();

		// 060009bc (1.10.1 RVA 0x6F2710): profiling is forced visible before the scene is read.
		public SceneInformation(Scene scene)
		{
			m_profilerManager.Get().StartGroupProfiling(true);
			SceneName = scene.name;
			GameObjectCounter = GetGameObjectCount(scene);
		}

		// 060009bd (1.10.1 RVA 0x6F2900): the snapshot covers inactive data too.
		public void CaptureGameSnapshot()
		{
			Snapshots.Add(m_profilerManager.Get().GetProfilerSnapshot(false));
		}

		// 060009be (1.10.1 RVA 0x6F29C0): the retail loop body is empty, so no snapshot is
		// output; only the enumeration and the forced stop remain.
		public void OutputData()
		{
			foreach (StringBuilder snapshot in Snapshots)
			{
			}
			m_profilerManager.Get().StopGroupProfiling(true);
		}

		// 060009bf (1.10.1 RVA 0x6F2890)
		private static int GetGameObjectCount(Scene scene)
		{
			int count = 0;
			foreach (GameObject rootGameObject in scene.GetRootGameObjects())
				count += GetTotalChildCount(rootGameObject.transform);
			return count;
		}

		// 060009c0 (1.10.1 RVA 0x6F2B10): the child count is read once; each child's subtree
		// is folded into the running count and the direct children are added last.
		private static int GetTotalChildCount(Transform t, int count = 1)
		{
			int childCount = t.childCount;
			if (childCount == 0) return count;
			for (int i = 0; i < childCount; i++)
				count = GetTotalChildCount(t.GetChild(i), count);
			return count + childCount;
		}
	}
}
