using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000a8a; bodies translated from the 1.10.1 x86-64 binary.
	// No runtime body reads the three expansion flags.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class LevelDataGameObjectActivation
	{
		// 06003ca8..06003cad (1.10.1 RVA 0x6CDF80..0x6CDFD0)
		public bool IsExpanded { get; set; }
		public bool IsExpandedEnable { get; set; }
		public bool IsExpandedDisable { get; set; }

		// 06003cae/06003caf (1.10.1 RVA 0x6CDFE0/0x6CDFF0); both lists are created by the
		// constructor 06003cb1 (0x6CE270).
		public List<GameObject> EnableGameObjects { get; } = new List<GameObject>();
		public List<GameObject> DisableGameObjects { get; } = new List<GameObject>();

		// 06003cb0 (1.10.1 RVA 0x6CE000): the enable list follows the flag and the disable list
		// its inverse; missing or destroyed objects are skipped.
		public void Apply(bool active)
		{
			foreach (GameObject gameObject in EnableGameObjects)
			{
				if (gameObject == null) continue;
				gameObject.SetActive(active);
			}
			foreach (GameObject gameObject in DisableGameObjects)
			{
				if (gameObject == null) continue;
				gameObject.SetActive(!active);
			}
		}
	}
}
