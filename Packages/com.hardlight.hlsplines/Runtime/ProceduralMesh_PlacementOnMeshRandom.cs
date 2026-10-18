using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLSplines.Runtime 0200002d; bodies translated from the 1.10.1 x86-64 binary.
	// Member order fixes the closure name <>c__DisplayClass14_0.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class ProceduralMesh_PlacementOnMeshRandom : ProceduralMesh_Placement
	{
		[SerializeField]
		private List<MeshCollider> m_meshColliders = new List<MeshCollider>(1);

		[SerializeField]
		private List<ProceduralMesh_TypeRandom> m_meshTypes = new List<ProceduralMesh_TypeRandom>(1);

		[SerializeField]
		private float m_numMeshes;

		[SerializeField]
		private bool m_scaleNumMeshes;

		private List<ProceduralMesh_StorageForMesh> m_storageInfo;

		// 060000af (1.10.1 RVA 0x1A5ADA0)
		private float NumMeshes => m_numMeshes;

		// 060000b0 (1.10.1 RVA 0x1A5ADB0)
		private bool ScaleNumMeshes => m_scaleNumMeshes;

		// 060000b1 (1.10.1 RVA 0x1A5ADC0)
		protected override IReadOnlyList<ProceduralMesh_Type> GetMeshTypes()
		{
			return m_meshTypes;
		}

		// 060000b2 (1.10.1 RVA 0x1A5ADD0)
		protected virtual void Reset()
		{
			MeshCollider meshCollider = gameObject.GetComponent<MeshCollider>();
			if (meshCollider != null)
				m_meshColliders.Add(meshCollider);
		}

		// 060000b3 (1.10.1 RVA 0x1A5AEC0). A NaN MaxRatio also fails the ratio check.
		protected override List<string> GetErrorMessages()
		{
			List<string> errors = null;
			bool hasMeshes = m_meshColliders.Count > 0;
			for (int i = 0; hasMeshes && i < m_meshColliders.Count; ++i)
				hasMeshes = m_meshColliders[i] != null;
			if (!hasMeshes)
				errors = AddErrorMessage(errors, "'" + name + "' has no mesh assigned");
			if (!(MaxRatio > 0f))
				errors = AddErrorMessage(errors, "'" + name + "' has no mesh types with a valid ratio. Set at least one to have a value greater than zero");

			IReadOnlyList<ProceduralMesh_Type> meshTypes = GetMeshTypes();
			for (int i = 0; i < meshTypes.Count; ++i)
			{
				if (!meshTypes[i].HasValidSetup())
					errors = AddErrorMessage(errors, string.Format("'{0}' has mesh type with invalid setup (Element {1}). {2}", name, i, meshTypes[i].GetErrorMessage()));
			}
			return errors;
		}

		// 060000b4 (1.10.1 RVA 0x1A5B250)
		internal override void InitialiseSetup()
		{
			base.InitialiseSetup();
			m_storageInfo = new List<ProceduralMesh_StorageForMesh>(m_meshColliders.Count);
			if (!HasValidSetup())
				return;

			ProceduralMesh_PlacementSetup.PerformanceSetup setup = Setup.GetSetup();
			bool precomputeLocations = SystemConfig.PrecomputeMeshLocations;
			for (int i = 0; i < m_meshColliders.Count; ++i)
			{
				var storageInfo = new ProceduralMesh_StorageForMesh(m_meshColliders[i], setup.NumMeshesMultiplier, setup.UseRuntimeCallback);
				m_storageInfo.Add(storageInfo);
				if (precomputeLocations)
					storageInfo.GenerateLocations(this);
			}
			TotalBounds = CalculateTotalWorldSpaceBounds();
		}

		// 060000b5 (1.10.1 RVA 0x1A5B6B0)
		protected override void DespawnAllMeshes(bool onDestroy = false, bool resetStorage = false, bool canFade = false)
		{
			if (m_populatedCount == 0 || m_storageInfo == null)
				return;

			m_populatedCount = 0;
			for (int i = 0; i < m_storageInfo.Count; ++i)
				m_populatedCount += m_storageInfo[i].DespawnSurfaceMeshes(onDestroy, resetStorage, canFade);
		}

		// 060000b6 (1.10.1 RVA 0x1A5B9A0). Mesh surfaces always count meshes per square metre.
		public override void GenerateLocations(ProceduralMesh_StorageForSubSurface storage, ISurface surface, Transform surfaceTransform)
		{
			float numMeshes = NumMeshes;
			bool scaleNumMeshes = ScaleNumMeshes;
			MeshSurface meshCollider = (MeshSurface)surface;
			storage.GenerateRandomLocations(surfaceTransform, m_meshTypes, NumMeshesType.PerMetreSquare, numMeshes, scaleNumMeshes, this,
				(random, numMeshesToCreate, startKnot, endKnot, meshType) => LocationGenerator.RandomPositions(random, meshCollider.Mesh.sharedMesh, numMeshesToCreate));
		}

		// 060000b7 (1.10.1 RVA 0x1A5BB40). The quote placement in the log text is original.
		protected override void CheckForSpawnAndDespawn(Camera camera, bool resetStorage)
		{
			if (m_storageInfo == null)
			{
				ValidSetup = false;
				HLOutput.LogError("'" + GetPath(transform) + " has invalid procedural mesh setup'");
				return;
			}

			m_populatedCount = 0;
			for (int i = 0; i < m_storageInfo.Count; ++i)
				m_populatedCount += m_storageInfo[i].CheckForSpawnAndDespawn(camera, SpawnDistanceSquare, DespawnDistanceSquare, FadeInDistanceSquare, FadeOutDistanceSquare, resetStorage, this);
		}

		// 060000b8 (1.10.1 RVA 0x1A5BD80). No meshes gives an empty bounds at the origin.
		protected override Bounds CalculateTotalWorldSpaceBounds()
		{
			Bounds bounds = default;
			if (m_storageInfo.Count > 0)
			{
				bounds = m_storageInfo[0].CalculateTotalWorldSpaceBounds();
				for (int i = 1; i < m_storageInfo.Count; ++i)
					bounds.Encapsulate(m_storageInfo[i].CalculateTotalWorldSpaceBounds());
			}
			return bounds;
		}

		// 060000b9 (1.10.1 RVA 0x1A5BFA0). The local mesh bounds; the knot range is ignored.
		public override Bounds CalculateBounds(ISurface surface, int startKnot, int endKnot)
		{
			return ((MeshSurface)surface).Mesh.sharedMesh.bounds;
		}
	}
}
