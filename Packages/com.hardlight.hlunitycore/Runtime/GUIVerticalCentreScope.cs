using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000271, methods 0x06000f73/0x06000f74:
    // a vertical group padded by flexible space on both sides.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class GUIVerticalCentreScope : IDisposable
    {
        private readonly bool m_enabled;

        public GUIVerticalCentreScope(bool condition = true)
        {
            m_enabled = condition;
            if (m_enabled)
            {
                GUILayout.BeginVertical();
                GUILayout.FlexibleSpace();
            }
        }

        public void Dispose()
        {
            if (m_enabled)
            {
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
            }
        }
    }
}
