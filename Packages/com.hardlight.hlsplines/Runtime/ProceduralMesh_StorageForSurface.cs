using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight
{
    // Original HLSplines.Runtime 0x02000049; bodies translated from the 1.10.1 x86-64 binary.
    // Unlike the other storage types this class keeps IL2CPP null checks.
    public class ProceduralMesh_StorageForSurface
    {
        protected List<ProceduralMesh_StorageForSubSurface> m_meshStorage;
        private int m_populatedCount;

        // The original body ignores every argument; derived constructors create the storage.
        protected ProceduralMesh_StorageForSurface(int startSubSurface, int endSubSurface, bool populatePerSubSurface, float numMeshesMultiplier, bool useRuntimeCallback)
        {
        }

        // One storage per sub-surface, or a single storage spanning the clamped range.
        protected void CreateMeshStorage(int startSubSurface, int endSubSurface, int subSurfaceCount, bool populatePerSubSurface, float numMeshesMultiplier, bool useRuntimeCallback)
        {
            m_meshStorage = new List<ProceduralMesh_StorageForSubSurface>();
            int start = Math.Max(startSubSurface, 0);
            int end = endSubSurface == ProceduralMesh_Constants.WholeSurface ? subSurfaceCount - 1 : Math.Min(endSubSurface, subSurfaceCount - 1);
            if (populatePerSubSurface)
            {
                for (int i = start; i < end; i++)
                {
                    m_meshStorage.Add(new ProceduralMesh_StorageForSubSurface
                    {
                        StartSubSurface = i,
                        EndSubSurface = i + 1,
                        SubSurfaceRange = subSurfaceCount - 1,
                        SubSurfaceSectionRange = end - start,
                        NumMeshesMultiplier = numMeshesMultiplier,
                        UseRuntimeCallback = useRuntimeCallback
                    });
                }
            }
            else
            {
                m_meshStorage.Add(new ProceduralMesh_StorageForSubSurface
                {
                    StartSubSurface = start,
                    EndSubSurface = end,
                    SubSurfaceRange = subSurfaceCount - 1,
                    SubSurfaceSectionRange = end - start,
                    NumMeshesMultiplier = numMeshesMultiplier,
                    UseRuntimeCallback = useRuntimeCallback
                });
            }
        }

        private bool CheckAllUnpopulated()
        {
            for (int i = 0; i < m_meshStorage.Count; i++)
            {
                if (m_meshStorage[i].State != ProceduralMesh_StorageForSubSurface.StorageState.Unpopulated)
                    return false;
            }
            return true;
        }

        // Returns the remaining populated count. With canFade, populated storage
        // starts (or continues) fading out instead of despawning at once.
        public int DespawnSurfaceMeshes(bool onDestroy = false, bool resetStorage = false, bool canFade = false, bool force = false)
        {
            if (m_populatedCount == 0 && !force)
                return 0;
            for (int i = 0; i < m_meshStorage.Count; i++)
            {
                ProceduralMesh_StorageForSubSurface storage = m_meshStorage[i];
                if (canFade)
                {
                    switch (storage.State)
                    {
                        case ProceduralMesh_StorageForSubSurface.StorageState.Unpopulated:
                            if (force)
                                storage.DespawnMeshes(onDestroy, resetStorage);
                            break;
                        case ProceduralMesh_StorageForSubSurface.StorageState.FadeIn:
                        case ProceduralMesh_StorageForSubSurface.StorageState.Populated:
                            if (storage.StartFadeOut(resetStorage))
                                m_populatedCount--;
                            break;
                        case ProceduralMesh_StorageForSubSurface.StorageState.FadeOut:
                            if (storage.FadeOut(resetStorage))
                                m_populatedCount--;
                            break;
                    }
                }
                else if (storage.State != ProceduralMesh_StorageForSubSurface.StorageState.Unpopulated)
                {
                    storage.DespawnMeshes(onDestroy, resetStorage);
                    m_populatedCount--;
                }
                else if (force)
                {
                    storage.DespawnMeshes(onDestroy, resetStorage);
                }
            }
            return m_populatedCount;
        }

        // Distances are measured in surface space; a camera beyond the despawn
        // distance despawns everything, fading when it is still within fade-out range.
        private bool IsTooFarAway(Transform surfaceTransform, Bounds bounds, Vector3 cameraPos, float despawnDistanceSquare, float fadeOutDistanceSquare, bool resetMeshStorage, out Vector3 cameraPosInSplineSpace)
        {
            Vector3 offset = cameraPos - surfaceTransform.position;
            cameraPosInSplineSpace = Quaternion.Inverse(surfaceTransform.rotation) * offset;
            float sqrDistance = bounds.SqrDistance(cameraPosInSplineSpace);
            if (sqrDistance > despawnDistanceSquare)
                DespawnSurfaceMeshes(false, resetMeshStorage, fadeOutDistanceSquare > sqrDistance);
            return sqrDistance > despawnDistanceSquare;
        }

        // With load balancing each call starts at most one spawn and at most one
        // despawn or fade-out; fade-outs already running can still finish.
        protected int CheckForSpawnAndDespawn(ISurface surface, Transform transform, List<ProceduralMesh_StorageForSubSurface> meshStorageList, Camera camera, float spawnDistanceSquare, float despawnDistanceSquare, float fadeInDistanceSquare, float fadeOutDistanceSquare, bool resetMeshStorage, ProceduralMesh_Placement owner)
        {
            Bounds bounds = surface.GetBoundingBox();
            Vector3 cameraPos = camera.transform.position;
            if (IsTooFarAway(transform, bounds, cameraPos, despawnDistanceSquare, fadeOutDistanceSquare, resetMeshStorage, out Vector3 cameraPosInSplineSpace))
                return m_populatedCount;

            bool canDespawn = true;
            bool canPopulate = true;
            for (int i = 0; i < meshStorageList.Count; i++)
            {
                ProceduralMesh_StorageForSubSurface storage = meshStorageList[i];
                float sqrDistance = storage.SquareDistanceToPoint(owner, surface, cameraPosInSplineSpace);
                switch (storage.State)
                {
                    case ProceduralMesh_StorageForSubSurface.StorageState.Unpopulated:
                        if (sqrDistance > spawnDistanceSquare || !canPopulate || !owner.TestAgainstCameraFrustum(transform, bounds))
                            break;
                        if (owner.SystemConfig.LoadBalancePopulate)
                            canPopulate = false;
                        storage.SpawnMeshes(surface, transform, sqrDistance > fadeInDistanceSquare, owner);
                        m_populatedCount++;
                        break;
                    case ProceduralMesh_StorageForSubSurface.StorageState.FadeIn:
                    case ProceduralMesh_StorageForSubSurface.StorageState.Populated:
                        if (canDespawn && sqrDistance > despawnDistanceSquare)
                        {
                            if (owner.SystemConfig.LoadBalanceUnpopulate)
                                canDespawn = false;
                            if (sqrDistance > fadeOutDistanceSquare)
                            {
                                storage.DespawnMeshes(false, resetMeshStorage);
                                m_populatedCount--;
                            }
                            else if (storage.StartFadeOut(resetMeshStorage))
                            {
                                m_populatedCount--;
                            }
                        }
                        else if (canDespawn && !owner.TestAgainstCameraFrustum(transform, bounds))
                        {
                            if (owner.SystemConfig.LoadBalanceUnpopulate)
                                canDespawn = false;
                            storage.DespawnMeshes(false, resetMeshStorage);
                            m_populatedCount--;
                        }
                        else if (storage.State == ProceduralMesh_StorageForSubSurface.StorageState.FadeIn)
                        {
                            storage.FadeIn();
                        }
                        break;
                    case ProceduralMesh_StorageForSubSurface.StorageState.FadeOut:
                        if (storage.FadeOut(resetMeshStorage))
                            m_populatedCount--;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            return m_populatedCount;
        }

        protected void MovementDetected()
        {
            DespawnSurfaceMeshes(false, true, false, true);
        }
    }
}
