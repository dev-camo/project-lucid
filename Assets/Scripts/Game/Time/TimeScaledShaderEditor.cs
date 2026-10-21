using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime TimeScaledShaderEditor (TypeDef 0x02000800, x86-64 0x5FB6C0..0x5FB78F): only the two
    // global time property names and the maximum survive in the shipped build; nothing reads them.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TimeScaledShaderEditor : MonoBehaviour
    {
        private static string[] m_timePropertyNames = { "_HLShaderTimeUI", "_HLShaderTimeGame" };
        private const float m_maxTime = 36000f;

        public TimeScaledShaderEditor() { } // 06002e5e; the initialiser is 06002e5f
    }
}
