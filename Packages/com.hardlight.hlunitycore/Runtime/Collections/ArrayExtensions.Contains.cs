using System;
namespace Hardlight
{
    // Maintained original ArrayExtensions dependency subset: one real method,
    // all original fields (none). The other ten original methods are unresolved.
    public static partial class ArrayExtensions
    {
        // Original HLUnityCore.Runtime06000247. Generic instances are context0;
        // array null reaches genuine Array.IndexOf validation without a guard.
        public static bool Contains<T>(this T[] array, T item)
        {
            return Array.IndexOf(array, item) >= 0;
        }
    }
}
