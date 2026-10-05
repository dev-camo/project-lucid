using System;

namespace Hardlight
{
    // Partial original API. Other extension methods are not recovered yet.
    public static class IGraphStorageExtensions
    {
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
