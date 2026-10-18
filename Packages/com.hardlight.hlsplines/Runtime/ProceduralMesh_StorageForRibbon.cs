using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Original HLSplines.Runtime 0x0200003f; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ProceduralMesh_StorageForRibbon : ProceduralMesh_StorageForSurface
    {
        private readonly Ribbon m_ribbon;

        // The base constructor ignores its arguments, so the native body does not show them.
        public ProceduralMesh_StorageForRibbon(Ribbon ribbon, int startKnot, int endKnot, bool populatePerKnot, float numMeshesMultiplier, bool useRuntimeCallback)
            : base(startKnot, endKnot, populatePerKnot, numMeshesMultiplier, useRuntimeCallback)
        {
            m_ribbon = ribbon;
            CreateMeshStorage(startKnot, endKnot, populatePerKnot, numMeshesMultiplier, useRuntimeCallback);
        }

        private void CreateMeshStorage(int startKnot, int endKnot, bool populatePerKnot, float numMeshesMultiplier, bool useRuntimeCallback)
        {
            CreateMeshStorage(startKnot, endKnot, m_ribbon.GetRibbon().KnotCount, populatePerKnot, numMeshesMultiplier, useRuntimeCallback);
        }

        public void GenerateLocations(ProceduralMesh_Placement owner)
        {
            Transform ribbonTransform = m_ribbon.transform;
            IRibbon ribbon = m_ribbon.GetRibbon();
            for (int i = 0; i < m_meshStorage.Count; i++)
                owner.GenerateLocations(m_meshStorage[i], ribbon, ribbonTransform);
        }

        public int CheckForSpawnAndDespawn(Camera camera, float spawnDistanceSquare, float despawnDistanceSquare, float fadeInDistanceSquare, float fadeOutDistanceSquare, bool resetMeshStorage, ProceduralMesh_Placement owner)
        {
            return CheckForSpawnAndDespawn(m_ribbon.GetRibbon(), m_ribbon.transform, m_meshStorage, camera, spawnDistanceSquare, despawnDistanceSquare, fadeInDistanceSquare, fadeOutDistanceSquare, resetMeshStorage, owner);
        }

        public Bounds CalculateTotalWorldSpaceBounds()
        {
            Bounds localBounds = m_ribbon.GetRibbon().GetBoundingBox();
            return ProceduralMesh_Placement.TransformBounds(m_ribbon.transform, localBounds);
        }
    }
}
