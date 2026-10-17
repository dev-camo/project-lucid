using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x0200026f, methods 0x06000f6e/0x06000f6f:
    // a horizontal group padded by flexible space on both sides.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class GUIHorizontalCentreScope : IDisposable
    {
        private readonly bool m_enabled;

        public GUIHorizontalCentreScope(bool condition = true)
        {
            m_enabled = condition;
            if (m_enabled)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
            }
        }

        public void Dispose()
        {
            if (m_enabled)
            {
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
        }
    }
}
