using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Original HLSplines.Runtime 0x0200004b; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Serializable]
    public class ProceduralMesh_Type
    {
        [SerializeField]
        private PrefabPoolType m_meshPoolType;

        [SerializeField]
        private int m_seed;

        [SerializeField]
        private float m_ratio;

        private SystemRef<PrefabPoolManager> m_manager;

        public ProceduralMesh_Pool MeshPool { get; set; }

        public PrefabPoolType MeshPoolType => m_meshPoolType;

        public int Seed
        {
            get => m_seed;
            set => m_seed = value;
        }

        public float Ratio => m_ratio;

        // 06000178 (1.10.1 RVA 0x1A63A20). Declared explicitly: seed 0 and ratio 1
        // are stored after the base constructor call (0x1A63A30).
        public ProceduralMesh_Type()
        {
            m_seed = 0;
            m_ratio = 1f;
        }

        // 0x06000179: the manager reference is resolved lazily and kept.
        public virtual bool HasValidSetup()
        {
            if (m_meshPoolType == null)
                return false;
            if (m_manager == null)
                m_manager = ProcessManager.GetSystemRef<PrefabPoolManager>();
            if (m_manager.IsNull())
                return false;
            return m_manager.Get().GetPoolByType(m_meshPoolType) != null;
        }

        public virtual string GetErrorMessage()
        {
            string errorMessage = "";
            if (m_meshPoolType == null)
            {
                errorMessage += "No mesh pool type assigned";
            }
            else
            {
                PrefabPoolManager manager = ProcessManager.GetSystemSafe<PrefabPoolManager>();
                if (manager == null)
                    errorMessage += "No ProceduralMeshPoolManager found in scene";
                else if (manager.GetPoolByType(m_meshPoolType) == null)
                    errorMessage += "No mesh pool found for type '" + m_meshPoolType.name + "'";
            }
            return errorMessage;
        }

        // 0x0600017b: a pool of another type leaves MeshPool null.
        public virtual void PrimeForUse()
        {
            PrefabPoolManager manager = ProcessManager.GetSystemSafe<PrefabPoolManager>();
            if (manager)
                MeshPool = manager.GetPoolByType(m_meshPoolType) as ProceduralMesh_Pool;
        }
    }
}
