using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Original HLSplines.Runtime 0x0200004c; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class ProceduralMesh_TypeGrid : ProceduralMesh_Type
    {
        [SerializeField]
        private ProceduralMesh_SetupTypeGrid m_setup;

        public ProceduralMesh_SetupTypeGrid Setup => m_setup;

        // 0x0600017d: a missing setup asset is replaced by a fresh default one.
        public override void PrimeForUse()
        {
            base.PrimeForUse();
            if (m_setup == null)
                m_setup = ScriptableObject.CreateInstance<ProceduralMesh_SetupTypeGrid>();
        }
    }
}
