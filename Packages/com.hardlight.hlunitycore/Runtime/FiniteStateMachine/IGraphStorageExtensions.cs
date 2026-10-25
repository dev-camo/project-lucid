using System;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000140, methods 0x060008bf..0x060008cb
    // in original declaration order. The class has no Il2CppSetOption, so
    // the shipped bodies keep their null checks.
    public static class IGraphStorageExtensions
    {
        // 0x060008bf; fully shared x86-64 0x950580. Unlike FillValueOnly, the
        // caller chooses whether a missing value is stored.
        public static void FillValue<T>(this IGraphStorage storage, GraphStorageKey storageKey, out T value, T defaultValue = default(T), bool storeDefault = true)
        { value = storage.GetValue(storageKey, defaultValue, storeDefault); }

        // HLUnityCore.Runtime.dll:0x060008c0; arm64 Single shared 0x933df0.
        // Read without inserting the supplied fallback.
        public static T GetValueOnly<T>(this IGraphStorage storage, GraphStorageKey storageKey, T defaultValue = default(T))
            => storage.GetValue(storageKey, defaultValue, false);

        // 0x060008c1; arm64 Boolean shared 0x9333a4. Assignment happens
        // after the storage call succeeds; an exception retains the prior out slot.
        public static void FillValueOnly<T>(this IGraphStorage storage, GraphStorageKey storageKey, out T value, T defaultValue = default(T))
        { value = storage.GetValue(storageKey, defaultValue, false); }

        // 0x060008c2..0x060008c4; x86-64 0x1aee410/0x1aee520/0x1aee630. Read
        // without default insertion, add with unchecked wrap (Int16 narrows),
        // then write and return the result.
        public static short AdjustValue(this IGraphStorage storage, GraphStorageKey storageKey, short adjustment, short defaultValue = 0)
        {
            short value = (short)(storage.GetValueOnly(storageKey, defaultValue) + adjustment);
            storage.SetValue(storageKey, value); return value;
        }

        public static int AdjustValue(this IGraphStorage storage, GraphStorageKey storageKey, int adjustment, int defaultValue = 0)
        {
            int value = storage.GetValueOnly(storageKey, defaultValue) + adjustment;
            storage.SetValue(storageKey, value); return value;
        }

        public static long AdjustValue(this IGraphStorage storage, GraphStorageKey storageKey, long adjustment, long defaultValue = 0)
        {
            long value = storage.GetValueOnly(storageKey, defaultValue) + adjustment;
            storage.SetValue(storageKey, value); return value;
        }

        // 0x060008c5; arm64 0x1af294c. Read without default insertion,
        // add using Single arithmetic, then write and return the resulting value.
        public static float AdjustValue(this IGraphStorage storage, GraphStorageKey storageKey, float adjustment, float defaultValue = 0f)
        {
            float value = storage.GetValueOnly(storageKey, defaultValue) + adjustment;
            storage.SetValue(storageKey, value); return value;
        }

        // 0x060008c6; x86-64 0x1aee870. Double arithmetic, same order.
        public static double AdjustValue(this IGraphStorage storage, GraphStorageKey storageKey, double adjustment, double defaultValue = 0)
        {
            double value = storage.GetValueOnly(storageKey, defaultValue) + adjustment;
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

        // 0x060008c8; fully shared x86-64 0x952630 (RGCTX TryGetValue<T>,
        // RemoveValue<T>). Remove only after a successful typed read, so a
        // conversion fault leaves the value stored.
        public static bool TryRemoveValue<T>(this IGraphStorage storage, GraphStorageKey storageKey, out T value)
        {
            if (!storage.TryGetValue(storageKey, out value)) return false;
            storage.RemoveValue<T>(storageKey);
            return true;
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
            catch (Exception exception)
            {
                // System.Exception handler (0x060008cb, x86-64 Boolean 0x94e6c5) logs before rethrowing.
                HLOutput.LogError("Failed to retrieve value with name '" + storageKey.ToString() + "' due to exception '" + exception.Message + "'");
                throw;
            }
        }
    }
}
