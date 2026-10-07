using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ProceduralMesh_Pool : PrefabPool<ProceduralMesh>
    {
        [SerializeField] private float m_fadeTimeSeconds;
        public float FadeTimeSeconds => m_fadeTimeSeconds;
    }
}
