using UnityEngine;

namespace Hardlight
{
    // HLUnityCore.Runtime0200006d: complete original fieldless extension owner.
    // C# emits the authentic ExtensionAttribute from each this parameter.
    public static class LayerMaskExtensions
    {
        // 060002a3. Original shift uses the low five bits, including out-of-range layers.
        public static void AddLayer(this ref LayerMask mask, int layer)
        {
            mask.value = mask.value | (1 << layer);
        }

        // 060002a4. Read and then publish through the real LayerMask value property.
        public static void RemoveLayer(this ref LayerMask mask, int layer)
        {
            mask.value = mask.value & ~(1 << layer);
        }

        // 060002a5. The original uses the implicit integer conversion.
        public static bool IncludesLayer(this LayerMask mask, int layer)
        {
            return (((int)mask) & (1 << layer)) != 0;
        }

        // 060002a6. Read gameObject.layer before conversion, with no Unity-null fallback.
        public static bool IncludesLayer(this LayerMask mask, GameObject gameObject)
        {
            int layer = gameObject.layer;
            return mask.IncludesLayer(layer);
        }
    }
}
