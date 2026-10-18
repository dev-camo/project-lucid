using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using Random = System.Random;

namespace Hardlight
{
    // Original HLSplines.Runtime 0x02000041; bodies translated from the 1.10.1 x86-64 binary.
    // Member order fixes the compiler-generated <>c__DisplayClass38_* names of SpawnFromLocations.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ProceduralMesh_StorageForSubSurface
    {
        private struct MeshLocation
        {
            public readonly Vector3 m_position;
            public readonly Quaternion m_rotation;
            public readonly float m_scale;
            public bool m_active;

            public MeshLocation(Vector3 pos, Quaternion rot, float scale)
            {
                m_position = pos;
                m_rotation = rot;
                m_scale = scale;
                m_active = true;
            }
        }

        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        private class MeshTypeStorage
        {
            public ProceduralMesh_Pool m_meshPool;
            public List<PooledPrefab<ProceduralMesh>> m_meshes;
            public List<MeshLocation> m_meshLocations;
        }

        public enum StorageState
        {
            Unpopulated,
            FadeIn,
            Populated,
            FadeOut
        }

        private Bounds m_bounds;
        private bool m_boundsCalculated;
        private List<MeshTypeStorage> m_meshTypeStorage;
        private float m_fadeTimestamp;

        public int StartSubSurface { get; set; }

        public int EndSubSurface { get; set; }

        public int SubSurfaceRange { get; set; }

        public int SubSurfaceSectionRange { get; set; }

        public float NumMeshesMultiplier { get; set; }

        public bool UseRuntimeCallback { get; set; }

        public StorageState State { get; private set; }

        public ProceduralMesh_StorageForSubSurface()
        {
            State = StorageState.Unpopulated;
            m_boundsCalculated = false;
        }

        private MeshTypeStorage CreateMeshStorage(ProceduralMesh_Type meshType, int numMeshes)
        {
            var storage = new MeshTypeStorage
            {
                m_meshPool = meshType.MeshPool,
                m_meshes = new List<PooledPrefab<ProceduralMesh>>(numMeshes),
                m_meshLocations = new List<MeshLocation>(numMeshes)
            };
            m_meshTypeStorage.Add(storage);
            return storage;
        }

        // Generated locations survive a despawn unless resetStorage drops them.
        public void DespawnMeshes(bool onDestroy = false, bool resetStorage = false)
        {
            if (State != StorageState.Unpopulated && m_meshTypeStorage != null)
            {
                foreach (MeshTypeStorage storage in m_meshTypeStorage)
                {
                    if (storage.m_meshPool != null)
                    {
                        for (int i = 0; i < storage.m_meshes.Count; i++)
                        {
                            if (onDestroy)
                                storage.m_meshPool.OnDestroyDespawnInstance(storage.m_meshes[i]);
                            else
                                storage.m_meshPool.DespawnInstance(storage.m_meshes[i]);
                        }
                    }
                    storage.m_meshes.Clear();
                }
                State = StorageState.Unpopulated;
            }
            if (resetStorage)
                m_meshTypeStorage = null;
        }

        // With UseRuntimeCallback each spawned mesh reports its enable state
        // back into its location; an exhausted pool stops that mesh type.
        private void SpawnFromLocations(bool fadeIn)
        {
            bool anyFading = false;
            for (int i = 0; i < m_meshTypeStorage.Count; i++)
            {
                MeshTypeStorage storage = m_meshTypeStorage[i];
                List<MeshLocation> meshLocations = storage.m_meshLocations;
                ProceduralMesh_Pool meshPool = storage.m_meshPool;
                bool fading = fadeIn && meshPool.FadeTimeSeconds > 0f;
                float initialScale = fading ? 0f : 1f;
                List<PooledPrefab<ProceduralMesh>> meshes = storage.m_meshes;
                int locationCount = meshLocations.Count;
                if (UseRuntimeCallback)
                {
                    for (int j = 0; j < locationCount; j++)
                    {
                        if (!meshPool.HasFreeInstance())
                        {
                            meshPool.MarkOverflow();
                            break;
                        }
                        MeshLocation location = meshLocations[j];
                        if (!location.m_active)
                            continue;
                        int idx = j;
                        var scale = new Vector3(location.m_scale, location.m_scale, location.m_scale);
                        PooledPrefab<ProceduralMesh> mesh = meshPool.SpawnInstance(location.m_position, location.m_rotation, scale, initialScale,
                            update =>
                            {
                                location.m_active = update == ObjectUpdateType.Enable;
                                meshLocations[idx] = location;
                            });
                        mesh.Instance.FinalScale = scale;
                        meshes.Add(mesh);
                    }
                }
                else
                {
                    for (int j = 0; j < locationCount; j++)
                    {
                        if (!meshPool.HasFreeInstance())
                        {
                            meshPool.MarkOverflow();
                            break;
                        }
                        MeshLocation location = meshLocations[j];
                        var scale = new Vector3(location.m_scale, location.m_scale, location.m_scale);
                        PooledPrefab<ProceduralMesh> mesh = meshPool.SpawnInstance(location.m_position, location.m_rotation, scale, initialScale);
                        mesh.Instance.FinalScale = scale;
                        meshes.Add(mesh);
                    }
                }
                anyFading |= fading;
            }
            m_fadeTimestamp = Time.time;
            State = anyFading ? StorageState.FadeIn : StorageState.Populated;
        }

        // Scales every mesh towards its final (fade in) or zero (fade out)
        // scale; returns true once no mesh type is still fading.
        private bool HandleFade(bool fadeIn)
        {
            int fadingCount = 0;
            float elapsed = Time.time - m_fadeTimestamp;
            float targetScale = fadeIn ? 1f : 0f;
            for (int i = 0; i < m_meshTypeStorage.Count; i++)
            {
                MeshTypeStorage storage = m_meshTypeStorage[i];
                float fadeTime = storage.m_meshPool.FadeTimeSeconds;
                if (fadeTime > 0f)
                {
                    float ratio = elapsed / fadeTime;
                    if (!fadeIn)
                        ratio = 1f - ratio;
                    if (elapsed > fadeTime)
                        ratio = targetScale;
                    else
                        fadingCount++;
                    int meshCount = storage.m_meshes.Count;
                    for (int j = 0; j < meshCount; j++)
                    {
                        PooledPrefab<ProceduralMesh> mesh = storage.m_meshes[j];
                        mesh.Instance.transform.localScale = mesh.Instance.FinalScale * ratio;
                    }
                }
            }
            return fadingCount == 0;
        }

        public void FadeIn()
        {
            if (HandleFade(true))
                State = StorageState.Populated;
        }

        // The original passes resetStorage as DespawnMeshes' onDestroy argument.
        public bool FadeOut(bool resetStorage = false)
        {
            bool finished = HandleFade(false);
            if (finished)
            {
                DespawnMeshes(resetStorage);
                State = StorageState.Unpopulated;
            }
            return finished;
        }

        public bool StartFadeOut(bool resetStorage = false)
        {
            m_fadeTimestamp = Time.time;
            State = StorageState.FadeOut;
            return FadeOut(resetStorage);
        }

        private static void UpdateRotation(ref Quaternion quat, Vector3 minRotationDeviation, Vector3 rotationDeviationVector, Random random)
        {
            var deviation = new Vector3((float)random.NextDouble() * rotationDeviationVector.x,
                (float)random.NextDouble() * rotationDeviationVector.y,
                (float)random.NextDouble() * rotationDeviationVector.z);
            quat *= Quaternion.Euler(minRotationDeviation + deviation);
        }

        private static void UpdatePosition(ref Vector3 pos, Vector3 minPositionDeviation, Vector3 positionDeviationVector, Random random, Quaternion quat)
        {
            var deviation = new Vector3((float)random.NextDouble() * positionDeviationVector.x,
                (float)random.NextDouble() * positionDeviationVector.y,
                (float)random.NextDouble() * positionDeviationVector.z);
            pos += quat * (minPositionDeviation + deviation);
        }

        // Bounds are computed once, on first use, in surface space.
        public float SquareDistanceToPoint(ProceduralMesh_Placement owner, ISurface surface, Vector3 point)
        {
            if (!m_boundsCalculated)
            {
                m_bounds = owner.CalculateBounds(surface, StartSubSurface, EndSubSurface);
                m_boundsCalculated = true;
            }
            return m_bounds.SqrDistance(point);
        }

        public void SpawnMeshes(ISurface surface, Transform surfaceTransform, bool fadeIn, ProceduralMesh_Placement owner)
        {
            DespawnMeshes();
            if (m_meshTypeStorage == null)
                owner.GenerateLocations(this, surface, surfaceTransform);
            SpawnFromLocations(fadeIn);
        }

        public delegate List<LightweightTransform> GetRandomLocations<in TMeshType>(Random random, int numMeshesToCreate, int startSubSurface, int endSubSurface, TMeshType meshType);

        public delegate List<LightweightTransform> GetGridLocations(int startSubSurface, int endSubSurface);

        public void GenerateRandomLocations<TMeshType>(Transform surfaceTransform, List<TMeshType> meshTypes, NumMeshesType numMeshesType, float numMeshes, bool scaleNumMeshes, ProceduralMesh_Placement owner, GetRandomLocations<TMeshType> getLocations)
            where TMeshType : ProceduralMesh_TypeRandom
        {
            int subSurfaceCount = EndSubSurface - StartSubSurface;
            switch (numMeshesType)
            {
                case NumMeshesType.PerKnot:
                    numMeshes *= subSurfaceCount;
                    break;
                case NumMeshesType.PerRibbon:
                    numMeshes = numMeshes * subSurfaceCount / SubSurfaceRange;
                    break;
                case NumMeshesType.PerSection:
                    numMeshes = numMeshes * subSurfaceCount / SubSurfaceSectionRange;
                    break;
                case NumMeshesType.PerMetreSquare:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(numMeshesType), numMeshesType, null);
            }

            float maxRatio = owner.MaxRatio;
            m_meshTypeStorage = new List<MeshTypeStorage>(meshTypes.Count);
            for (int i = 0; i < meshTypes.Count; i++)
            {
                TMeshType meshType = meshTypes[i];
                ProceduralMesh_SetupTypeRandom setup = meshType.Setup;
                Vector3 minRotationDeviation = setup.MinRotationDeviation;
                float minHeightDeviation = setup.MinHeightDeviation;
                float heightDeviationRange = setup.HeightDeviationRange;
                Vector3 rotationDeviationVector = setup.RotationDeviationVector;
                float minScale = setup.MinScale;
                float scaleRange = setup.ScaleRange;
                bool useParentScale = setup.UseParentScale;
                Quaternion surfaceRotation = surfaceTransform.rotation;
                Matrix4x4 localToWorld = surfaceTransform.localToWorldMatrix;
                Vector3 localScale = surfaceTransform.localScale;
                float parentScale = useParentScale ? Mathf.Min(localScale.x, localScale.z) : 1f;
                // A unit parent scale (either sign) with no scale range keeps minScale
                // without drawing; the native tests the offset against both -float.Epsilon
                // and float.Epsilon, so NaN offsets and ranges also keep minScale.
                float scaleOffset = Mathf.Abs(parentScale) - 1f;
                float areaScale = scaleNumMeshes ? localScale.x * localScale.z : 1f;
                int numMeshesToCreate = Mathf.RoundToInt(meshType.Ratio / maxRatio * numMeshes * areaScale * NumMeshesMultiplier);
                int seed = meshType.Seed;
                if (seed == 0)
                    seed = owner.GetInstanceID();
                var random = new Random(seed + i + StartSubSurface);
                List<LightweightTransform> locations = getLocations(random, numMeshesToCreate, StartSubSurface, EndSubSurface, meshType);
                List<MeshLocation> meshLocations = CreateMeshStorage(meshType, locations.Count).m_meshLocations;
                for (int j = 0; j < locations.Count; j++)
                {
                    LightweightTransform location = locations[j];
                    Vector3 position = location.Location;
                    Quaternion rotation = location.Orientation;
                    if (rotationDeviationVector.sqrMagnitude > float.Epsilon)
                        UpdateRotation(ref rotation, minRotationDeviation, rotationDeviationVector, random);
                    rotation = surfaceRotation * rotation;
                    if (heightDeviationRange > float.Epsilon)
                    {
                        float height = (float)random.NextDouble() * heightDeviationRange + minHeightDeviation;
                        position += rotation * new Vector3(0f, height, 0f);
                    }
                    Vector3 worldPosition = localToWorld.MultiplyPoint3x4(position);
                    float scale = minScale;
                    if (scaleRange > float.Epsilon || scaleOffset > float.Epsilon || scaleOffset < -float.Epsilon)
                        scale = ((float)random.NextDouble() * scaleRange + minScale) * parentScale;
                    meshLocations.Add(new MeshLocation(worldPosition, rotation, scale));
                }
            }
        }

        // Grid locations are shared out between the mesh types by ratio; with
        // gaps or several types they are shuffled first, then trimmed by gapsRatio.
        public void GenerateGridLocations<TMeshType>(Transform surfaceTransform, List<TMeshType> meshTypes, float gapsRatio, ProceduralMesh_Placement owner, GetGridLocations getLocations)
            where TMeshType : ProceduralMesh_TypeGrid
        {
            float maxRatio = owner.MaxRatio;
            m_meshTypeStorage = new List<MeshTypeStorage>(meshTypes.Count);
            List<LightweightTransform> locations = getLocations(StartSubSurface, EndSubSurface);
            if (gapsRatio > 0f || meshTypes.Count >= 2)
            {
                int shuffleSeed = meshTypes[0].Seed != 0 ? meshTypes[0].Seed : owner.GetInstanceID();
                locations.Shuffle(new Random(shuffleSeed + StartSubSurface));
            }
            if (gapsRatio > 0f)
            {
                int numberToKeep = Mathf.FloorToInt((1f - Math.Min(gapsRatio, 1f)) * locations.Count);
                locations.RemoveRange(numberToKeep, locations.Count - numberToKeep);
            }

            int locationIndex = 0;
            for (int i = 0; i < meshTypes.Count; i++)
            {
                TMeshType meshType = meshTypes[i];
                int numMeshes = (int)Math.Floor(meshType.Ratio / maxRatio * locations.Count);
                int seed = meshType.Seed != 0 ? meshType.Seed : owner.GetInstanceID();
                var random = new Random(seed + i + StartSubSurface);
                MeshTypeStorage storage = CreateMeshStorage(meshType, numMeshes);
                ProceduralMesh_SetupTypeGrid setup = meshType.Setup;
                Vector3 minPositionDeviation = setup.MinPositionDeviation;
                Vector3 minRotationDeviation = setup.MinRotationDeviation;
                Vector3 positionDeviationVector = setup.PositionDeviationVector;
                Vector3 rotationDeviationVector = setup.RotationDeviationVector;
                float minScale = setup.MinScale;
                float scaleRange = setup.ScaleRange;
                bool useParentScale = setup.UseParentScale;
                Quaternion surfaceRotation = surfaceTransform.rotation;
                Matrix4x4 localToWorld = surfaceTransform.localToWorldMatrix;
                List<MeshLocation> meshLocations = storage.m_meshLocations;
                Vector3 localScale = surfaceTransform.localScale;
                float parentScale = useParentScale ? Mathf.Min(localScale.x, localScale.z) : 1f;
                float scaleOffset = Mathf.Abs(parentScale) - 1f;
                for (int j = 0; j < numMeshes; j++)
                {
                    LightweightTransform location = locations[locationIndex];
                    Vector3 position = location.Location;
                    Quaternion rotation = location.Orientation;
                    if (rotationDeviationVector.sqrMagnitude > float.Epsilon)
                        UpdateRotation(ref rotation, minRotationDeviation, rotationDeviationVector, random);
                    rotation = surfaceRotation * rotation;
                    if (positionDeviationVector.sqrMagnitude > float.Epsilon)
                        UpdatePosition(ref position, minPositionDeviation, positionDeviationVector, random, rotation);
                    Vector3 worldPosition = localToWorld.MultiplyPoint3x4(position);
                    float scale = minScale;
                    if (scaleRange > float.Epsilon || scaleOffset > float.Epsilon || scaleOffset < -float.Epsilon)
                        scale = ((float)random.NextDouble() * scaleRange + minScale) * parentScale;
                    meshLocations.Add(new MeshLocation(worldPosition, rotation, scale));
                    locationIndex++;
                }
            }
        }
    }
}
