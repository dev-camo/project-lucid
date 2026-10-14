using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Assembly-CSharp; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [ExecuteInEditMode]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class DreamGetPlayerPositionDebug : MonoBehaviour
    {
        private Shader dreamEffect;
        private Vector3 PlayerPosition;

        private const string playerPosition = "_playerPosition";

        // 06000046. The found shader is kept but never used.
        private void Start()
        {
            dreamEffect = Shader.Find("HL_Dream_Object_RenderFeature");
        }

        // 06000047
        private void Update()
        {
            PlayerPosition = gameObject.transform.position;
            Shader.SetGlobalVector(playerPosition, PlayerPosition);
        }
    }
}
