using System;

namespace Hardlight
{
    public partial class EnumUtilities
    {
        // HLUnityCore.Runtime.dll:0x06000f60; shared arm64 0x8e760c.
        // Enum.Parse(Type, string) is case-sensitive and accepts numeric values
        // outside the named members. The native catch uses System.Object,
        // returning the supplied fallback for any managed parsing/cast failure.
        public static T SafeParse<T>(string enumName, T fallback)
        {
            try { return (T)Enum.Parse(typeof(T), enumName); }
            catch { return fallback; }
        }
    }
}
