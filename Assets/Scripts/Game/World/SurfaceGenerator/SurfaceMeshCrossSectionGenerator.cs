using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HardlightProject
{
    // Original Game.Runtime 02000aa2 with nested KnotStepData 02000aa3; bodies translated
    // from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public abstract class SurfaceMeshCrossSectionGenerator
    {
        public class KnotStepData
        {
            public Vector3 m_origin;
            public Quaternion m_rotation;
        }

        [SerializeField]
        [Min(0.01f)]
        private float m_width;

        [SerializeField]
        private Vector3 m_offsetLocal;

        [SerializeField]
        private bool m_prefabSpawnGenerate;

        [SerializeField]
        private Transform m_prefabSpawnRoot;

        [SerializeField]
        private List<SurfaceMeshPrefabSpawner> m_prefabSpawners = new List<SurfaceMeshPrefabSpawner>();

        protected float Width => m_width;

        protected Vector3 OffsetLocal => m_offsetLocal;

        public abstract SurfaceCrossSectionType Type { get; }

        public abstract Vector3 GetOrigin(KnotStepData knotStepData, float offsetMultiplier);

        public abstract void GenerateMeshCrossSection(List<Vector3> vertices, List<int> triangles, KnotStepData knotStepData,
            int knotStepIndex, int knotStepCount, bool invertOrder, float offsetMultiplier = 0f);

        protected abstract void CalculateLocationLocal(float distance, out Vector3 positionLocal, out Quaternion rotationLocal);

        // 06003d39 (0x6D37A0): (v2, v1, v0)(v1, v2, v3) when inverted, else (v0, v1, v2)(v3, v2, v1).
        protected void AddQuad(int v0, int vStride, bool invertOrder, List<int> triangles)
        {
            int v1 = v0 + 1;
            int v2 = v0 + vStride;
            int v3 = v0 + vStride + 1;
            if (invertOrder)
            {
                triangles.Add(v2);
                triangles.Add(v1);
                triangles.Add(v0);
                triangles.Add(v1);
                triangles.Add(v2);
                triangles.Add(v3);
            }
            else
            {
                triangles.Add(v0);
                triangles.Add(v1);
                triangles.Add(v2);
                triangles.Add(v3);
                triangles.Add(v2);
                triangles.Add(v1);
            }
        }

        // 06003d3a (0x6D3F80): a pending request clears the spawn root and respawns every
        // spawner from OffsetLocal.z to Distance + OffsetLocal.z; zero spacing is skipped.
        // The request flag is cleared only after every spawner has run.
        public void SpawnPrefabs(IRibbon ribbon, KnotStepData knotOrigin = null)
        {
            if (!m_prefabSpawnGenerate || m_prefabSpawnRoot == null)
                return;
            for (int i = m_prefabSpawnRoot.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(m_prefabSpawnRoot.GetChild(i).gameObject);
            Vector3 origin = knotOrigin != null ? knotOrigin.m_origin : Vector3.zero;
            foreach (SurfaceMeshPrefabSpawner prefabSpawner in m_prefabSpawners)
            {
                if (prefabSpawner.Spacing == 0f)
                    continue;
                Vector3 offsetLocal = prefabSpawner.OffsetLocal;
                float distanceEnd = prefabSpawner.Distance + offsetLocal.z;
                for (float distance = offsetLocal.z; distance <= distanceEnd; distance += prefabSpawner.Spacing)
                {
                    RibbonLocation ribbonLocation = ribbon.GetRibbonLocationFromDistance(distance, RibbonAlignment.Center);
                    LightweightTransform knotTransform = ribbonLocation.GetLocalTransform();
                    CalculateLocationLocal(offsetLocal.x, out Vector3 positionLocal, out Quaternion rotationLocal);
                    Vector3 offset = rotationLocal * (positionLocal + Vector3.up * offsetLocal.y);
                    string label = string.Format("({0:F2}, {1:F2}, {2:F2})", offsetLocal.x, offsetLocal.y, distance);
                    prefabSpawner.Spawn(knotTransform.Location + origin + offset, knotTransform.Orientation, label, m_prefabSpawnRoot);
                }
            }
            m_prefabSpawnGenerate = false;
        }
    }
}
