using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Original HLSplines.Runtime 0x02000040; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ProceduralMesh_StorageForSpline : ProceduralMesh_StorageForSurface
    {
        private readonly Spline m_spline;

        // The base constructor ignores its arguments, so the native body does not show them.
        public ProceduralMesh_StorageForSpline(Spline spline, int startKnot, int endKnot, bool populatePerKnot, float numMeshesMultiplier, bool useRuntimeCallback)
            : base(startKnot, endKnot, populatePerKnot, numMeshesMultiplier, useRuntimeCallback)
        {
            m_spline = spline;
            CreateMeshStorage(startKnot, endKnot, populatePerKnot, numMeshesMultiplier, useRuntimeCallback);
        }

        private void CreateMeshStorage(int startKnot, int endKnot, bool populatePerKnot, float numMeshesMultiplier, bool useRuntimeCallback)
        {
            CreateMeshStorage(startKnot, endKnot, m_spline.GetSpline().KnotCount, populatePerKnot, numMeshesMultiplier, useRuntimeCallback);
        }

        public void GenerateLocations(ProceduralMesh_Placement owner)
        {
            Transform splineTransform = m_spline.transform;
            ISpline spline = m_spline.GetSpline();
            for (int i = 0; i < m_meshStorage.Count; i++)
                owner.GenerateLocations(m_meshStorage[i], spline, splineTransform);
        }

        public int CheckForSpawnAndDespawn(Camera camera, float spawnDistanceSquare, float despawnDistanceSquare, float fadeInDistanceSquare, float fadeOutDistanceSquare, bool resetMeshStorage, ProceduralMesh_Placement owner)
        {
            return CheckForSpawnAndDespawn(m_spline.GetSpline(), m_spline.transform, m_meshStorage, camera, spawnDistanceSquare, despawnDistanceSquare, fadeInDistanceSquare, fadeOutDistanceSquare, resetMeshStorage, owner);
        }

        public Bounds CalculateTotalWorldSpaceBounds()
        {
            Bounds localBounds = m_spline.GetSpline().GetBoundingBox();
            return ProceduralMesh_Placement.TransformBounds(m_spline.transform, localBounds);
        }
    }
}
