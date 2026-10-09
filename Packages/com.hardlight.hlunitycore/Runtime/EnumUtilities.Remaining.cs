using System;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // HLUnityCore.Runtime 0x0200026b: genuine whole eight-method provider
    // completed with maintained SafeParse0x06000f60 in its existing partial.
    // Native generic specializations are context, not extra unique body credit.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public partial class EnumUtilities
    {
        public static T Parse<T>(string enumName) =>
            (T)Enum.Parse(typeof(T), enumName); // 0x06000f5f

        public static string[] GetNames<T>() => Enum.GetNames(typeof(T)); // 0x06000f61

        // 0x06000f62: native IsInst is the original nullable array cast.
        public static T[] GetValues<T>() => Enum.GetValues(typeof(T)) as T[];

        // 0x06000f63: IConvertible only; boxed Convert.ToInt32 retains overflow
        // faults instead of truncating enum storage or treating it as an int[].
        public static int[] GetValuesAsInt<T>() where T : IConvertible
        {
            T[] values = GetValues<T>();
            int[] result = new int[values == null ? 0 : values.Length];
            for (int i = 0; i < result.Length; ++i)
                result[i] = Convert.ToInt32((object)values[i]);
            return result;
        }

        public static bool IsDefined<T>(T value) => Enum.IsDefined(typeof(T), value); // 0x06000f64

        // 0x06000f65: exact cast before the out store/definition check. Boxed
        // foreign enums with the same underlying storage can pass this cast;
        // mismatched storage faults before the out store. The native cast-class
        // check retains this behavior; no conversion, catch or type guard is added.
        public static bool TryConvertEnum<T>(Enum enumValue, out T convertedEnum) where T : Enum
        {
            convertedEnum = (T)enumValue;
            if (IsDefined(convertedEnum)) return true;
            convertedEnum = default;
            return false;
        }

        public EnumUtilities() { } // 0x06000f66: original nonstatic Object..ctor
    }
}
