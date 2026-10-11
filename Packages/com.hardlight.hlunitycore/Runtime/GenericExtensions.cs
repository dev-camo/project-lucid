// Preserved Sonic Dream Team 1.10.1 original 0x0600027f native evidence.
// ARM64 and x86_64 registered body addresses: 0x90e470, 0x90e6fc, 0x92d8d0, 0x92db60.
// Original HLUnityCore.Runtime.dll Hardlight.GenericExtensions 02000064/IsObsolete0600027f.
// Enum constraint has no struct flag. Runtime type/name lookup precedes obsolete test.
// Equal-valued nonobsolete aliases make an obsolete selected field return false.
// Undefined-name/null faults and reflection iteration order are retained; local names inferred.
// Open definition is zero; registered shared roles remain separate from closed ScreenOrientation.
// Source spelling, optimizer/fault/provider/runtime/current emission equivalence held.
using System;
using System.Reflection;

namespace Hardlight
{
    public static class GenericExtensions
    {
        public static bool IsObsolete<T>(this T value) where T : Enum
        {
            Type type = value.GetType();
            FieldInfo selected = type.GetField(type.GetEnumName(value));
            if (!Attribute.IsDefined(selected, typeof(ObsoleteAttribute)))
                return false;

            foreach (FieldInfo field in type.GetFields())
            {
                if (field.Equals(selected) || field.FieldType != type)
                    continue;
                if (!field.GetValue(null).Equals(value))
                    continue;
                if (!Attribute.IsDefined(field, typeof(ObsoleteAttribute)))
                    return false;
            }
            return true;
        }
    }
}
