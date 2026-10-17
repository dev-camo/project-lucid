using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 02000250 (06000eea..06000eeb), bodies from the
    // 1.10.1 x86-64 binary. The cache is a mutable static that GetString extends
    // with every unnamed value it formats.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public static class EnumExtensions
    {
        // BeforeFieldInit with .cctor 0x06000eeb (0x1b1b1a0): a field initialiser
        // keyed by EnumComparers.UpdateOnComparer, in this Add order.
        private static Dictionary<UpdateOn, string> UpdateOnEnumToString = new Dictionary<UpdateOn, string>(EnumComparers.UpdateOnComparer)
        {
            { UpdateOn.FixedUpdate, "FixedUpdate" },
            { UpdateOn.LateUpdate, "LateUpdate" },
            { UpdateOn.None, "None" },
            { UpdateOn.Update, "Update" },
        };

        // 0x06000eea (0x1b1b090): an unknown value is cached as "0x{0:X}".
        public static string GetString(this UpdateOn value)
        {
            if (!UpdateOnEnumToString.TryGetValue(value, out string text))
            {
                text = string.Format("0x{0:X}", value);
                UpdateOnEnumToString[value] = text;
            }
            return text;
        }
    }
}
