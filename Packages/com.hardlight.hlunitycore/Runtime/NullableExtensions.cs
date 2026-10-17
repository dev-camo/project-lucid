namespace Hardlight
{
    // Original HLUnityCore.Runtime 02000075 (060002d0..060002d1). Only the fully
    // shared struct instantiations shipped (0x982380, 0x982460); their method
    // RGCTX tables name GetValueOrDefault, HasValue and TryGetValue<T>.
    public static class NullableExtensions
    {
        // 0x060002d0: the value is written before HasValue is read, so an empty
        // nullable still yields default(T).
        public static bool TryGetValue<T>(this T? nullable, out T nullableValue) where T : struct
        {
            nullableValue = nullable.GetValueOrDefault();
            return nullable.HasValue;
        }

        // 0x060002d1.
        public static T TryGetWithDefault<T>(this T? nullable, T defaultValue = default) where T : struct
        {
            return nullable.TryGetValue(out T value) ? value : defaultValue;
        }
    }
}
