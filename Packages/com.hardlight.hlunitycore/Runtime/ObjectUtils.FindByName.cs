using UnityEngine;

namespace Hardlight.Utils
{
    // Maintained genuine subset of original ObjectUtils (zero fields,35 methods).
    // Other original methods remain unresolved; this is not complete type recovery.
    public partial class ObjectUtils
    {
        // Original HLUnityCore.Runtime06001236, ARM0x968dd0.
        public static T FindByName<T>(string name) where T : Object => FindByNameSafe<T>(name);

        // Original06001237, ARM0x968e20. Null/empty names avoid engine discovery.
        // Keep Resources order, first exact name and native null-element failure.
        public static T FindByNameSafe<T>(string name) where T : Object
        {
            if (string.IsNullOrEmpty(name)) return null;
            T[] objects = Resources.FindObjectsOfTypeAll<T>();
            foreach (T current in objects)
                if (current.name == name) return current;
            return null;
        }
        // Original06001238 is the genuine parameterless Object constructor.
    }
}
