using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class InstancedObjectPoolManager : MonoBehaviour, ISystem
    {
        [SerializeField, Min(0)]
        private int m_defaultMeshCapacity = 10;
        private readonly InstancedObjectPool<Mesh> m_meshPool = new InstancedObjectPool<Mesh>();

        // Original 0x06003d00/0x06003d01; arm64 0x6ad29c/0x6ad30c.
        private void Awake() => ProcessManager.RegisterSystem(this);
        private void OnDestroy() => ProcessManager.UnregisterSystem(this);

        // Original 0x06003d02..0x06003d04; arm64 0x6ad394/0x6ad3f4/0x6ad460.
        public int RegisterMesh(Mesh mesh) => m_meshPool.Register(mesh, m_defaultMeshCapacity);
        public bool GetMesh(int id, out Mesh mesh) => m_meshPool.Get(id, out mesh);
        public void ReleaseMesh(int id, Mesh mesh) => m_meshPool.Release(id, mesh);

        // Original 0x06003d05 implicit constructor initializes capacity then mesh pool before MonoBehaviour's base constructor.
        // Native destruction unregisters this system; it does not clear or dispose the retained pools.
    }
}
