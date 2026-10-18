using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000a7e; bodies translated from the 1.10.1 x86-64 binary.
	// The constructor 06003c55 (0x6C9BB0) allocates no collider list.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[RequireComponent(typeof(BoxCollider))]
	public class HashedColliderGroup : HashedGroup
	{
		[Tooltip("All colliders that are located inside the box collider.")]
		[SerializeField]
		private List<Collider> m_colliderList;
		[SerializeField]
		[Tooltip("Hash of all serialized colliders for optimisation check.")]
		private int m_collidersHashCode;

		// 06003c54 (1.10.1 RVA 0x6C9A70): every listed collider is set, with no null test.
		public void OnTrigger(bool active)
		{
			foreach (Collider collider in m_colliderList)
				collider.enabled = active;
		}
	}
}
