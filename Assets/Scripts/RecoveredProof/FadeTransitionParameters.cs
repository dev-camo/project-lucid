using System;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200082b, complete three-method declaration.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class FadeTransitionParameters
    {
        // 0x06002f76/0x06002f77; ARM 0x5e00c8/0x5e00d0.
        // Genuine readonly auto-properties; no normalization or invocation.
        public FadeTransitionType Type { get; }
        public Action OnMidpointCallback { get; }

        // 0x06002f78; ARM 0x5e00d8. Object base precedes the assignments;
        // retain any enum value and the exact supplied delegate, including null.
        public FadeTransitionParameters(FadeTransitionType type, Action onMidpointCallback)
        {
            Type = type;
            OnMidpointCallback = onMidpointCallback;
        }
    }
}
