using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Original HLSplines.Runtime 0x0200003e; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ProceduralMesh_StorageForMesh : ProceduralMesh_StorageForSurface
    {
        private readonly MeshCollider m_meshCollider;
        private readonly MeshSurface m_meshSurface;

        // The base constructor ignores its arguments, so the native body does not show them.
        public ProceduralMesh_StorageForMesh(MeshCollider meshCollider, float numMeshesMultiplier, bool useRuntimeCallback)
            : base(0, ProceduralMesh_Constants.WholeSurface, false, numMeshesMultiplier, useRuntimeCallback)
        {
            m_meshCollider = meshCollider;
            m_meshSurface = new MeshSurface(meshCollider);
            CreateMeshStorage(numMeshesMultiplier, useRuntimeCallback);
        }

        // A mesh is a single two-point surface, so it always gets one storage.
        private void CreateMeshStorage(float numMeshesMultiplier, bool useRuntimeCallback)
        {
            CreateMeshStorage(0, ProceduralMesh_Constants.WholeSurface, 2, false, numMeshesMultiplier, useRuntimeCallback);
        }

        public void GenerateLocations(ProceduralMesh_Placement owner)
        {
            Transform meshTransform = m_meshCollider.transform;
            for (int i = 0; i < m_meshStorage.Count; i++)
                owner.GenerateLocations(m_meshStorage[i], m_meshSurface, meshTransform);
        }

        public int CheckForSpawnAndDespawn(Camera camera, float spawnDistanceSquare, float despawnDistanceSquare, float fadeInDistanceSquare, float fadeOutDistanceSquare, bool resetMeshStorage, ProceduralMesh_Placement owner)
        {
            Transform meshTransform = m_meshCollider.transform;
            return CheckForSpawnAndDespawn(m_meshSurface, meshTransform, m_meshStorage, camera, spawnDistanceSquare, despawnDistanceSquare, fadeInDistanceSquare, fadeOutDistanceSquare, resetMeshStorage, owner);
        }

        public Bounds CalculateTotalWorldSpaceBounds()
        {
            return m_meshCollider.bounds;
        }
    }
}
