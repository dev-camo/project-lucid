using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000aa5; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class SurfaceMeshPrefabSpawner
    {
        [SerializeField]
        [Tooltip("Prefab to spawn.")]
        private GameObject m_prefab;

        [SerializeField]
        [Tooltip("Spacing of spawned prefabs.")]
        [Min(0.1f)]
        private float m_spacing = 1f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Distance of spawned prefabs. 0 specifies entire length.")]
        private float m_distance;

        [SerializeField]
        [Tooltip("Local offset applied from base mesh.")]
        private Vector3 m_offsetLocal;

        [SerializeField]
        [Tooltip("Scale of spawned prefabs.")]
        [Min(0.1f)]
        private float m_scale = 1f;

        public float Spacing => m_spacing;

        public float Distance => m_distance;

        public Vector3 OffsetLocal => m_offsetLocal;

        // 06003d46 (0x6D4660): InstantiatePrefab is inlined as null, so the shipped body fails
        // on the first instance access before any transform is touched.
        public void Spawn(Vector3 positionLocal, Quaternion rotationLocal, string label, Transform spawnRoot)
        {
            GameObject instance = InstantiatePrefab(m_prefab, spawnRoot);
            instance.name = instance.name + " " + label;
            instance.transform.localPosition = positionLocal;
            instance.transform.localRotation = rotationLocal;
            instance.transform.localScale = Vector3.one * m_scale;
        }

        // 06003d47 (0x6D4D10): only the shipped player body (`xor eax, eax; ret`) is recoverable.
        // Any editor-only instantiation branch was compiled out of retail and is not restored, so
        // Spawn faults in the Editor as well, although an editor build of the original may have spawned.
        public static GameObject InstantiatePrefab(GameObject prefab, Transform parent)
        {
            return null;
        }
    }
}
