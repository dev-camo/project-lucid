using System.Collections.Generic;
namespace Hardlight
{
    public static partial class ListExtensions
    {
        // HLUnityCore.Runtime.dll:Hardlight.ListExtensions:0x060002c3;
        // shared arm64 0x9530dc. Null list and collection callback errors escape.
        public static bool AddUnique<T>(this IList<T> list, T entry)
        {
            bool contains = list.Contains(entry);
            if (!contains) list.Add(entry);
            return !contains;
        }
    }
}
