using System;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000270, methods 0x06000f70..0x06000f72.
    // The skin is captured even when the condition leaves it unchanged, so
    // Dispose always restores the captured skin.
    public readonly struct GUISkinScope : IDisposable
    {
        public readonly GUISkin OriginalSkin;

        // 0x06000f70.
        public GUISkinScope(bool condition, GUISkin newSkin)
        {
            OriginalSkin = GUI.skin;
            if (condition)
                GUI.skin = newSkin;
        }

        // 0x06000f71: the same capture and unconditional assignment (the
        // retail body is the inlined chained constructor).
        public GUISkinScope(GUISkin newSkin) : this(true, newSkin) { }

        // 0x06000f72.
        public void Dispose()
        {
            GUI.skin = OriginalSkin;
        }
    }
}
