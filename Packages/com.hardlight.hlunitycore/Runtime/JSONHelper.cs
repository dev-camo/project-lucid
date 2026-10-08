using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.JSON
{
    // Original HLUnityCore.Runtime 0x02000299, whole sixteen-method owner.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class JSONHelper
    {
        // 0x0400084b and 0x060010b7. Original mutable lookup and Add order;
        // field initializer retains BeforeFieldInit rather than an explicit static constructor.
        private static Dictionary<Type, int> s_poolSizeLookup = new Dictionary<Type, int>
        {
            { typeof(JSONBool), 150 },
            { typeof(JSONDouble), 100 },
            { typeof(JSONHashtable), 200 },
            { typeof(JSONLong), 500 },
            { typeof(JSONString), 1000 },
            { typeof(JSONArray), 200 }
        };

        // 0x060010a8. Original default pool size, with original dictionary null-key fault.
        public static int GetPoolSizeForType(Type type)
        {
            int size;
            return s_poolSizeLookup.TryGetValue(type, out size) ? size : 100;
        }

        // 0x060010a9. Original compatible owners and out initialization.
        public static bool TryConvertToType(IJsonObject encodable, out float value)
        {
            value = 0;
            JSONDouble candidate0 = encodable as JSONDouble;
            if (candidate0 != null)
            {
                value = (float)candidate0.Value;
                return true;
            }
            JSONLong candidate1 = encodable as JSONLong;
            if (candidate1 != null)
            {
                value = (float)candidate1.Value;
                return true;
            }
            return false;
        }

        // 0x060010aa. The default is an original required argument.
        public static float ConvertToType(IJsonObject encodable, float defaultIfIncompatible)
        {
            float value;
            return TryConvertToType(encodable, out value) ? value : defaultIfIncompatible;
        }

        // 0x060010ab. Original compatible owners and out initialization.
        public static bool TryConvertToType(IJsonObject encodable, out double value)
        {
            value = 0;
            JSONDouble candidate0 = encodable as JSONDouble;
            if (candidate0 != null)
            {
                value = candidate0.Value;
                return true;
            }
            JSONLong candidate1 = encodable as JSONLong;
            if (candidate1 != null)
            {
                value = (double)candidate1.Value;
                return true;
            }
            return false;
        }

        // 0x060010ac. The default is an original required argument.
        public static double ConvertToType(IJsonObject encodable, double defaultIfIncompatible)
        {
            double value;
            return TryConvertToType(encodable, out value) ? value : defaultIfIncompatible;
        }

        // 0x060010ad. Original compatible owners and out initialization.
        public static bool TryConvertToType(IJsonObject encodable, out int value)
        {
            value = 0;
            JSONLong candidate0 = encodable as JSONLong;
            if (candidate0 != null)
            {
                value = unchecked((int)candidate0.Value);
                return true;
            }
            return false;
        }

        // 0x060010ae. The default is an original required argument.
        public static int ConvertToType(IJsonObject encodable, int defaultIfIncompatible)
        {
            int value;
            return TryConvertToType(encodable, out value) ? value : defaultIfIncompatible;
        }

        // 0x060010af. Original compatible owners and out initialization.
        public static bool TryConvertToType(IJsonObject encodable, out long value)
        {
            value = 0;
            JSONLong candidate0 = encodable as JSONLong;
            if (candidate0 != null)
            {
                value = candidate0.Value;
                return true;
            }
            return false;
        }

        // 0x060010b0. The default is an original required argument.
        public static long ConvertToType(IJsonObject encodable, long defaultIfIncompatible)
        {
            long value;
            return TryConvertToType(encodable, out value) ? value : defaultIfIncompatible;
        }

        // 0x060010b1. Original compatible owners and out initialization.
        public static bool TryConvertToType(IJsonObject encodable, out string value)
        {
            value = null;
            JSONString candidate0 = encodable as JSONString;
            if (candidate0 != null)
            {
                value = candidate0.Value;
                return true;
            }
            return false;
        }

        // 0x060010b2. The default is an original required argument.
        public static string ConvertToType(IJsonObject encodable, string defaultIfIncompatible)
        {
            string value;
            return TryConvertToType(encodable, out value) ? value : defaultIfIncompatible;
        }

        // 0x060010b3. Original string conversion uses Boolean.Parse and preserves its faults.
        public static bool TryConvertToType(IJsonObject encodable, out bool value)
        {
            value = false;
            JSONBool boolean = encodable as JSONBool;
            if (boolean != null)
            {
                value = boolean.Value;
                return true;
            }
            if (encodable is JSONString)
            {
                value = bool.Parse(ConvertToType(encodable, "false"));
                return true;
            }
            return false;
        }

        // 0x060010b4. The default is an original required argument.
        public static bool ConvertToType(IJsonObject encodable, bool defaultIfIncompatible)
        {
            bool value;
            return TryConvertToType(encodable, out value) ? value : defaultIfIncompatible;
        }

        // 0x060010b5. Original nullable unwrap, exact target order, enum parse and catch/log.
        public static object GetBasicTypeFromJsonObject(IJsonObject encodable, Type requiredType)
        {
            try
            {
                Type underlying = Nullable.GetUnderlyingType(requiredType);
                if (underlying != null) requiredType = underlying;
                if (requiredType == typeof(float))
                {
                    float value;
                    if (TryConvertToType(encodable, out value)) return value;
                }
                else if (requiredType == typeof(double))
                {
                    double value;
                    if (TryConvertToType(encodable, out value)) return value;
                }
                else if (requiredType == typeof(long))
                {
                    long value;
                    if (TryConvertToType(encodable, out value)) return value;
                }
                else if (requiredType == typeof(int))
                {
                    int value;
                    if (TryConvertToType(encodable, out value)) return value;
                }
                else if (requiredType == typeof(string))
                {
                    string value;
                    if (TryConvertToType(encodable, out value)) return value;
                }
                else if (requiredType == typeof(bool))
                {
                    bool value;
                    if (TryConvertToType(encodable, out value)) return value;
                }
                else if (requiredType.IsEnum)
                {
                    string value;
                    if (TryConvertToType(encodable, out value)) return Enum.Parse(requiredType, value);
                }
            }
            catch (Exception exception)
            {
                HLOutput.LogError(exception.Message);
            }
            return null;
        }

        // 0x060010b6. Original object-first/array-fallback, PopulateObject default20,
        // as-T null conversion, Add semantics and foreach disposal; no catch introduced.
        public static Dictionary<string, T> JsonToDictionary<T>(JSONHashtable json) where T : class
        {
            if (json == null) return null;
            Dictionary<string, T> result = new Dictionary<string, T>();
            foreach (string key in json.Keys)
            {
                IJsonObject value = json.GetObject(key);
                if (value == null) value = json.GetArray(key);
                object populated = JSONSerializer.PopulateObject(typeof(T), value);
                result.Add(key, populated as T);
            }
            return result;
        }
    }
}
