using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ProceduralMesh : MonoBehaviour
    {
        [SerializeField] public Vector3 FinalScale = Vector3.one;
    }
}
