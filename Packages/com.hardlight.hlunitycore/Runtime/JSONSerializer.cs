using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.JSON
{
    // Original HLUnityCore.Runtime 0x0200029c; complete twenty-eight-method owner.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class JSONSerializer
    {
        // Original fields 0x0400084e..0x04000859.
        public const int TOKEN_NONE = 0;
        public const int TOKEN_CURLY_OPEN = 1;
        public const int TOKEN_CURLY_CLOSE = 2;
        public const int TOKEN_SQUARED_OPEN = 3;
        public const int TOKEN_SQUARED_CLOSE = 4;
        public const int TOKEN_COLON = 5;
        public const int TOKEN_COMMA = 6;
        public const int TOKEN_STRING = 7;
        public const int TOKEN_NUMBER = 8;
        public const int TOKEN_TRUE = 9;
        public const int TOKEN_FALSE = 10;
        public const int TOKEN_NULL = 11;

        // 0x0400085a/0x0400085b / 0x060010e1, in original initializer order.
        // The shared builder and derived-type cache are mutable and unsynchronized.
        private static StringBuilder s_tempStringBuilder = new StringBuilder(10240);
        private static Dictionary<Type, List<Type>> s_derivedTypes = new Dictionary<Type, List<Type>>();

        // Original nested 0x0200029d.
        [Flags]
        public enum EncodeOptions { None = 0, SkipNull = 1 }

        // 0x060010c6. Original byte decoding uses ASCII, including its replacement behavior.
        public static object Decode(byte[] json) { return Decode(Encoding.ASCII.GetString(json)); }

        // 0x060010c7.
        public static IJsonObject Decode(string json)
        {
            bool success = true;
            return Decode(json, ref success);
        }

        // 0x060010c8. Original decoded object is not released by this overload.
        public static void Decode(object instance, string json, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance)
        {
            IJsonObject obj = Decode(json);
            PopulateObject(instance.GetType(), obj, instance, bindingFlags);
        }

        // 0x060010c9. A null input logs while leaving success true.
        public static IJsonObject Decode(string json, ref bool success)
        {
            success = true;
            if (json != null)
            {
                char[] characters = json.ToCharArray();
                int index = 0;
                return ParseValue(characters, ref index, ref success);
            }
            HLOutput.LogError("Failed to decode json.");
            return null;
        }

        // 0x060010ca. Reentry may overwrite the shared builder; no new lock or guard.
        public static string Encode(object json, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance, EncodeOptions options = EncodeOptions.None)
        {
            s_tempStringBuilder.Length = 0;
            return SerializeValue(json, s_tempStringBuilder, bindingFlags, options) ? s_tempStringBuilder.ToString() : null;
        }

        // 0x060010cb.
        public static T Decode<T>(byte[] json) where T : class, new()
        {
            return Decode<T>(Encoding.ASCII.GetString(json), BindingFlags.Public | BindingFlags.Instance);
        }

        // 0x060010cc. Release follows population and uses object identity, without finally.
        public static T Decode<T>(string json, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance) where T : class, new()
        {
            bool success = true;
            IJsonObject obj = Decode(json, ref success);
            if (success)
            {
                T result = PopulateObject(typeof(T), obj, bindingFlags) as T;
                if ((object)result != obj) obj.Release();
                if (result != null) return result;
            }
            HLOutput.LogError("Failed to decode json: " + json);
            return null;
        }

        // 0x060010cd. Reflection loading faults propagate; exact base types are excluded.
        private static List<Type> GetDerivedTypes(Type baseType)
        {
            List<Type> result = new List<Type>();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                foreach (Type type in assembly.GetTypes())
                    if (type.IsSubclassOf(baseType)) result.Add(type);
            return result;
        }

        // 0x060010ce.
        public static object PopulateObject(Type T, IJsonObject obj, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance)
        {
            return PopulateObject(T, obj, null, bindingFlags);
        }

        // 0x060010cf. Original reflection paths only populate fields from JSONHashtable.
        // Dictionary/list enumeration failures fall through to basic conversion; their
        // construction, generic-argument reads and array assignments are outside those catches.
        // The two narrow catch/finally regions are inferred from both native LSDA maps.
        private static object PopulateObject(Type T, IJsonObject obj, object instance, BindingFlags bindingFlags)
        {
            if (obj == null) return null;
            if (T.IsAssignableFrom(obj.GetType())) return obj;
            if (obj is JSONHashtable)
            {
                JSONHashtable table = (JSONHashtable)obj;
                if (table.ContainsKey("typeOf"))
                {
                    Type derived = GetTypeFromName(JSONHelper.ConvertToType(table.Get("typeOf"), (string)null), T);
                    if (derived != null) T = derived;
                }
                if (instance == null) instance = Activator.CreateInstance(T);
                foreach (FieldInfo field in T.GetFields(bindingFlags))
                {
                    if (table.ContainsKey(field.Name))
                        field.SetValue(instance, PopulateObject(field.FieldType, table.Get(field.Name), null, bindingFlags));
                }
                return instance;
            }
            if (obj is JSONDictionary)
            {
                if (instance == null) instance = Activator.CreateInstance(T);
                Type[] arguments = T.GetGenericArguments();
                Type keyType = arguments[0];
                Type valueType = arguments[1];
                IDictionary dictionary = instance as IDictionary;
                JSONDictionary table = (JSONDictionary)obj;
                IEnumerator<KeyValuePair<IJsonObject, IJsonObject>> enumerator = table.GetEnumerator();
                try
                {
                    while (enumerator.MoveNext())
                    {
                        IJsonObject key = enumerator.Current.Key;
                        object convertedKey = JSONHelper.GetBasicTypeFromJsonObject(key, keyType);
                        if (convertedKey != null)
                            dictionary.Add(convertedKey, PopulateObject(valueType, table.Get(key), null, bindingFlags));
                    }
                    return instance;
                }
                catch { }
                finally { if (enumerator != null) enumerator.Dispose(); }
            }
            if (obj is JSONArray)
            {
                JSONArray jsonArray = (JSONArray)obj;
                if (T.IsArray)
                {
                    if (instance == null) instance = Activator.CreateInstance(T, new object[] { jsonArray.Count });
                    Array array = (Array)instance;
                    for (int index = 0; index < jsonArray.Count; index++)
                        array.SetValue(PopulateObject(T.GetElementType(), jsonArray[index], null, bindingFlags), index);
                    return instance;
                }
                if (instance == null) instance = Activator.CreateInstance(T);
                IList list = instance as IList;
                if (list == null) return instance;
                Type itemType = typeof(object);
                Type instanceType = instance.GetType();
                if (instanceType.IsGenericType)
                {
                    Type[] arguments = instanceType.GetGenericArguments();
                    if (arguments.Length != 1) return null;
                    itemType = arguments[0];
                }
                IEnumerator<IJsonObject> enumerator = ((IEnumerable<IJsonObject>)obj).GetEnumerator();
                try
                {
                    while (enumerator.MoveNext())
                        list.Add(PopulateObject(itemType, enumerator.Current, null, bindingFlags));
                    return instance;
                }
                catch { }
                finally { if (enumerator != null) enumerator.Dispose(); }
            }
            object basic = JSONHelper.GetBasicTypeFromJsonObject(obj, T);
            if (basic != null)
            {
                if (T.IsAssignableFrom(basic.GetType())) instance = basic;
            }
            else HLOutput.LogError("Couldn't read JSON");
            return instance;
        }

        // 0x060010d0. True format markers select the dictionary parser.
        public static IJsonObject ParseValue(char[] json, ref int index, ref bool success)
        {
            switch (LookAhead(json, index))
            {
                case TOKEN_CURLY_OPEN:
                    return JSONDictionary.CheckType(json, ref index, success)
                        ? (IJsonObject)JSONDictionary.Create(json, ref index, ref success)
                        : JSONHashtable.Create(json, ref index, ref success);
                case TOKEN_SQUARED_OPEN: return JSONArray.Create(json, ref index, ref success);
                case TOKEN_STRING: return JSONString.Create(json, ref index, ref success);
                case TOKEN_NUMBER: return ParseNumber(json, ref index, ref success);
                case TOKEN_TRUE: NextToken(json, ref index); return JSONBool.Create(true);
                case TOKEN_FALSE: NextToken(json, ref index); return JSONBool.Create(false);
                case TOKEN_NULL: NextToken(json, ref index); return null;
                default: success = false; return null;
            }
        }

        // 0x060010d1. Only a decimal point selects double parsing. Failed numbers
        // become JSONString and do not update success; cursor advancement precedes parsing.
        private static IJsonObject ParseNumber(char[] json, ref int index, ref bool success)
        {
            double doubleValue = 0;
            long longValue = 0;
            EatWhitespace(json, ref index);
            int lastIndex = GetLastIndexOfNumber(json, index);
            string value = new string(json, index, lastIndex - index + 1);
            index = lastIndex + 1;
            if (value.Contains("."))
                return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out doubleValue)
                    ? (IJsonObject)JSONDouble.Create(doubleValue) : JSONString.Create(value);
            return long.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out longValue)
                ? (IJsonObject)JSONLong.Create(longValue) : JSONString.Create(value);
        }

        // 0x060010d2. Opening position is consumed without validating a quote.
        // Unknown escapes are dropped. Original Unicode conversion uses ConvertFromUtf32,
        // so surrogate code points fault rather than being silently combined or repaired.
        public static string ParseString(char[] json, ref int index, ref bool success)
        {
            StringBuilder builder = new StringBuilder();
            uint codePoint = 0;
            EatWhitespace(json, ref index);
            index++;
            if (index == json.Length) { success = false; return null; }
            do
            {
                char character = json[index++];
                if (character == '"') return builder.ToString();
                if (character == '\\')
                {
                    if (index == json.Length) { success = false; return null; }
                    character = json[index++];
                    switch (character)
                    {
                        case '"': builder.Append('"'); break;
                        case '/': builder.Append('/'); break;
                        case '\\': builder.Append('\\'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u':
                            if (json.Length - index < 4) { success = false; return null; }
                            success = uint.TryParse(new string(json, index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out codePoint);
                            if (!success) return string.Empty;
                            builder.Append(char.ConvertFromUtf32(unchecked((int)codePoint)));
                            index += 4;
                            break;
                    }
                }
                else builder.Append(character);
            } while (index != json.Length);
            success = false;
            return null;
        }

        // 0x060010d3.
        public static int GetLastIndexOfNumber(char[] json, int index)
        {
            while (index < json.Length && "0123456789+-.eE".IndexOf(json[index]) != -1) index++;
            return index - 1;
        }

        // 0x060010d4.
        public static void EatWhitespace(char[] json, ref int index)
        {
            while (index < json.Length && " \t\n\r".IndexOf(json[index]) != -1) index++;
        }

        // 0x060010d5. Lookahead advances a local cursor only.
        public static int LookAhead(char[] json, int index) { return NextToken(json, ref index); }

        // 0x060010d6. Literal prefixes require no following delimiter.
        // Numeric token recognition consumes one character, as in the original table.
        public static int NextToken(char[] json, ref int index)
        {
            EatWhitespace(json, ref index);
            if (index == json.Length) return TOKEN_NONE;
            char character = json[index++];
            switch (character)
            {
                case '{': return TOKEN_CURLY_OPEN;
                case '}': return TOKEN_CURLY_CLOSE;
                case '[': return TOKEN_SQUARED_OPEN;
                case ']': return TOKEN_SQUARED_CLOSE;
                case ',': return TOKEN_COMMA;
                case ':': return TOKEN_COLON;
                case '"': return TOKEN_STRING;
                case '-': case '0': case '1': case '2': case '3': case '4':
                case '5': case '6': case '7': case '8': case '9': return TOKEN_NUMBER;
            }
            index--;
            int remaining = json.Length - index;
            if (remaining >= 5 && json[index] == 'f' && json[index + 1] == 'a' && json[index + 2] == 'l' && json[index + 3] == 's' && json[index + 4] == 'e')
            { index += 5; return TOKEN_FALSE; }
            if (remaining >= 4)
            {
                if (json[index] == 't' && json[index + 1] == 'r' && json[index + 2] == 'u' && json[index + 3] == 'e')
                { index += 4; return TOKEN_TRUE; }
                if (json[index] == 'n' && json[index + 1] == 'u' && json[index + 2] == 'l' && json[index + 3] == 'l')
                { index += 4; return TOKEN_NULL; }
            }
            return TOKEN_NONE;
        }

        // 0x060010d7. Original cached discovery retains the first simple-name match.
        public static Type GetTypeFromName(string typeName, Type baseType)
        {
            if (!s_derivedTypes.ContainsKey(baseType)) s_derivedTypes[baseType] = GetDerivedTypes(baseType);
            List<Type> types = s_derivedTypes[baseType];
            for (int index = 0; index < types.Count; index++)
                if (types[index].Name == typeName) return types[index];
            return null;
        }

        // 0x060010d8. Dispatch order, reflection getter faults and partial builder writes
        // follow the original. DateTime uses an unquoted round-trip string; enum text is unescaped.
        private static bool SerializeValue(object value, StringBuilder builder, BindingFlags bindingFlags, EncodeOptions options)
        {
            if (value is IJsonObject) return ((IJsonObject)value).Encode(builder);
            if (value is string) { SerializeString((string)value, builder); return true; }
            if (value is Hashtable) return SerializeObject((Hashtable)value, builder, bindingFlags, options);
            if (value is IDictionary) return SerializeObject((IDictionary)value, builder, bindingFlags, options);
            if (value is IEnumerable) return SerializeArray((IEnumerable)value, builder, bindingFlags, options);
            if (value == null) { builder.Append("null"); return true; }
            if (value is float) { SerializeNumber(Convert.ToSingle(value), builder); return true; }
            if (value is int || value is long || value is uint) { SerializeNumber(Convert.ToInt64(value), builder); return true; }
            if (value is double) { SerializeNumber(Convert.ToDouble(value), builder); return true; }
            if (value is bool && (bool)value) { builder.Append("true"); return true; }
            if (value is bool && !(bool)value) { builder.Append("false"); return true; }
            if (value is DateTime) { builder.Append(((DateTime)value).ToString("o")); return true; }
            if (value is Enum) { builder.Append("\"" + value.ToString() + "\""); return true; }
            builder.Append("{");
            bool first = true;
            foreach (FieldInfo field in value.GetType().GetFields(bindingFlags))
            {
                if (field.IsNotSerialized) continue;
                object fieldValue = field.GetValue(value);
                if ((options & EncodeOptions.SkipNull) != 0 && fieldValue == null) continue;
                if (!first) builder.Append(", ");
                SerializeString(field.Name, builder);
                builder.Append(":");
                if (!SerializeValue(fieldValue, builder, bindingFlags, options)) return false;
                first = false;
            }
            foreach (PropertyInfo property in value.GetType().GetProperties())
            {
                object propertyValue = property.GetValue(value, null);
                if ((options & EncodeOptions.SkipNull) != 0 && propertyValue == null) continue;
                if (!first) builder.Append(", ");
                SerializeString(property.Name, builder);
                builder.Append(":");
                if (!SerializeValue(propertyValue, builder, bindingFlags, options)) return false;
                first = false;
            }
            builder.Append("}");
            return true;
        }

        // 0x060010d9. Key.ToString precedes Value and SkipNull. Closing brace precedes disposal.
        public static bool SerializeObject(Hashtable anObject, StringBuilder builder, BindingFlags bindingFlags, EncodeOptions options)
        {
            builder.Append("{");
            bool first = true;
            IDictionaryEnumerator enumerator = anObject.GetEnumerator();
            IDisposable disposable = enumerator as IDisposable;
            try
            {
                while (enumerator.MoveNext())
                {
                    string key = enumerator.Key.ToString();
                    object value = enumerator.Value;
                    if ((options & EncodeOptions.SkipNull) != 0 && value == null) continue;
                    if (!first) builder.Append(", ");
                    SerializeString(key, builder);
                    builder.Append(":");
                    if (!SerializeValue(value, builder, bindingFlags, options)) return false;
                    first = false;
                }
                builder.Append("}");
                return true;
            }
            finally { if (disposable != null) disposable.Dispose(); }
        }

        // 0x060010da. Dictionary keys use SerializeValue and ignore its boolean result.
        public static bool SerializeObject(IDictionary anObject, StringBuilder builder, BindingFlags bindingFlags, EncodeOptions options)
        {
            builder.Append("{");
            builder.Append("\""); builder.Append(JSONDictionary.TokenDictionaryKey); builder.Append("\"");
            builder.Append(":");
            builder.Append("\""); builder.Append(JSONDictionary.TokenDictionaryValue); builder.Append("\"");
            IDictionaryEnumerator enumerator = anObject.GetEnumerator();
            IDisposable disposable = enumerator as IDisposable;
            try
            {
                while (enumerator.MoveNext())
                {
                    object key = enumerator.Key;
                    object value = enumerator.Value;
                    if ((options & EncodeOptions.SkipNull) != 0 && value == null) continue;
                    builder.Append(", ");
                    SerializeValue(key, builder, bindingFlags, options);
                    builder.Append(":");
                    if (!SerializeValue(value, builder, bindingFlags, options)) return false;
                }
                builder.Append("}");
                return true;
            }
            finally { if (disposable != null) disposable.Dispose(); }
        }

        // 0x060010db. Closing bracket follows disposal; null array elements remain present.
        public static bool SerializeArray(IEnumerable anArray, StringBuilder builder, BindingFlags bindingFlags, EncodeOptions options)
        {
            builder.Append("[");
            bool first = true;
            IEnumerator enumerator = anArray.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    object value = enumerator.Current;
                    if (!first) builder.Append(", ");
                    if (!SerializeValue(value, builder, bindingFlags, options)) return false;
                    first = false;
                }
            }
            finally
            {
                IDisposable disposable = enumerator as IDisposable;
                if (disposable != null) disposable.Dispose();
            }
            builder.Append("]");
            return true;
        }

        // 0x060010dc. Original ASCII-printable interval is [32,127), and Unicode escapes
        // use lowercase four-digit UTF-16 units. Null faults after the opening quote.
        public static bool SerializeString(string aString, StringBuilder builder)
        {
            builder.Append("\"");
            foreach (char character in aString.ToCharArray())
            {
                switch (character)
                {
                    case '\b': builder.Append("\\b"); break;
                    case '\t': builder.Append("\\t"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    default:
                        int code = Convert.ToInt32(character);
                        if (code >= 32 && code < 127) builder.Append(character);
                        else builder.Append("\\u" + Convert.ToString(code, 16).PadLeft(4, '0'));
                        break;
                }
            }
            builder.Append("\"");
            return true;
        }

        // 0x060010dd.
        public static bool SerializeNumber(int number, StringBuilder builder)
        { builder.Append(Convert.ToString(number, CultureInfo.InvariantCulture)); return true; }
        // 0x060010de.
        public static bool SerializeNumber(float number, StringBuilder builder)
        { builder.Append(Convert.ToString(number, CultureInfo.InvariantCulture)); return true; }
        // 0x060010df.
        public static bool SerializeNumber(long number, StringBuilder builder)
        { builder.Append(Convert.ToString(number, CultureInfo.InvariantCulture)); return true; }
        // 0x060010e0.
        public static bool SerializeNumber(double number, StringBuilder builder)
        { builder.Append(Convert.ToString(number, CultureInfo.InvariantCulture)); return true; }
    }
}
