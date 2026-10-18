using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000a12; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class MergeColliders : MonoBehaviour
	{
		// 060039b6 (1.10.1 RVA 0x6B2560)
		protected void Awake()
		{
			DoMergeColliders();
		}

		// 060039b7 (1.10.1 RVA 0x6B2570): child meshes are combined in world space into a new
		// mesh on this object and each child's own collider is destroyed. The MeshCollider is
		// added while the combined MeshFilter exists, and that filter is then destroyed.
		private void DoMergeColliders()
		{
			MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
			if (meshFilters.Length == 0) return;

			CombineInstance[] combine = new CombineInstance[meshFilters.Length];
			for (int i = 0; i < meshFilters.Length; i++)
			{
				MeshFilter meshFilter = meshFilters[i];
				combine[i].mesh = meshFilter.sharedMesh;
				combine[i].transform = meshFilter.transform.localToWorldMatrix;
				Collider meshCollider = meshFilter.GetComponent<Collider>();
				if (meshCollider != null)
					Destroy(meshCollider);
			}

			MeshFilter combinedFilter = gameObject.AddComponent<MeshFilter>();
			combinedFilter.mesh = new Mesh();
			combinedFilter.mesh.CombineMeshes(combine);
			gameObject.AddComponent<MeshCollider>();
			Destroy(combinedFilter);
		}
	}
}
