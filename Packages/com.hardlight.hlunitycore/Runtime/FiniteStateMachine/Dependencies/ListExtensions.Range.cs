using System.Collections.Generic;

namespace Hardlight
{
    public static partial class ListExtensions
    {
        // HLUnityCore.Runtime.dll 0x060002c4; reference shared ARM640x95348c.
        // Every AddUnique call executes after the first successful addition;
        // enumeration failure retains earlier additions and finally disposal.
        public static bool AddUniqueFromRange<T>(this IList<T> list, IReadOnlyCollection<T> entries)
        {
            bool changed = false;
            foreach (T entry in entries) changed |= AddUnique(list, entry);
            return changed;
        }
    }
}
