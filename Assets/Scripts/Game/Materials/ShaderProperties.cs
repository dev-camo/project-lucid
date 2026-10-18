using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020006ce. A name, not a property ID: callers pass
    // it to the string overloads of the Material accessors.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public static class ShaderProperties
    {
        public static readonly string DreamNoiseST = "_Dream_Noise_ST";
    }
}
