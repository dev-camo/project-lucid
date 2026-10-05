using System;

namespace Hardlight
{
    // Partial original API. The remaining numeric overloads/value helpers
    // are unresolved; these retained bodies cover authored Boolean/time conditions.
    public static class IGraphStorageExtensions
    {
        // HLUnityCore.Runtime.dll:0x060008c0; arm64 Single shared 0x933df0.
        // Read without inserting the supplied fallback.
        public static T GetValueOnly<T>(this IGraphStorage storage, GraphStorageKey storageKey, T defaultValue = default(T))
            => storage.GetValue(storageKey, defaultValue, false);

        // 0x060008c1; arm64 Boolean shared 0x9333a4. Assignment happens
        // after the storage call succeeds; an exception retains the prior out slot.
        public static void FillValueOnly<T>(this IGraphStorage storage, GraphStorageKey storageKey, out T value, T defaultValue = default(T))
        { value = storage.GetValue(storageKey, defaultValue, false); }

        // 0x060008c5; arm64 0x1af294c. Read without default insertion,
        // add using Single arithmetic, then write and return the resulting value.
        public static float AdjustValue(this IGraphStorage storage, GraphStorageKey storageKey, float adjustment, float defaultValue = 0f)
        {
            float value = storage.GetValueOnly(storageKey, defaultValue) + adjustment;
            storage.SetValue(storageKey, value); return value;
        }

        // 0x060008c7; arm64 Boolean/reference shared 0x934494/0x934954.
        // A present value is converted with the original exception semantics;
        // missing assigns default(T), with no storage insertion or coercion.
        public static bool TryGetValue<T>(this IGraphStorage storage, GraphStorageKey storageKey, out T value)
        {
            bool found = storage.GetCollection().TryGetValue(storageKey, out object raw);
            value = found ? storage.ConvertValue<T>(storageKey, raw) : default(T);
            return found;
        }

        // 0x060008c9; arm64 0x1af2b9c: untyped key presence only.
        public static bool HasValue(this IGraphStorage storage, GraphStorageKey storageKey) => storage.GetCollection().ContainsKey(storageKey);

        // 0x060008ca; arm64 Boolean/reference shared 0x934074/0x9340d8.
        // Typed presence performs conversion, including its failure behavior.
        public static bool HasValue<T>(this IGraphStorage storage, GraphStorageKey storageKey) => storage.TryGetValue<T>(storageKey, out _);

        // HLUnityCore.Runtime.dll:Hardlight.IGraphStorageExtensions:0x060008cb;
        // arm64 generic reference 0x931f9c / int 0x93181c.
        public static T ConvertValue<T>(this IGraphStorage storage, GraphStorageKey storageKey, object value)
        {
            try
            {
                if (value is IGraphStorage.Var<T> wrapped) return wrapped.Value;
                return (T)value;
            }
            catch (InvalidCastException exception)
            {
                // Native logs the original key and exception message, then rethrows.
                HLOutput.LogError("Failed to retrieve value with name '" + storageKey.ToString() + "' due to exception '" + exception.Message + "'");
                throw;
            }
        }
    }
}
