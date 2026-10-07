using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace ProjectLucid.Editor
{
    public static class OriginalSerializedDictionaryFamilyVerification
    {
        private static int checks;
        private static void Check(bool condition, string witness)
        { if (!condition) throw new InvalidOperationException(witness); checks++; }
        private static void Throws<T>(Action action, string witness) where T : Exception
        {
            try { action(); } catch (T) { checks++; return; }
            throw new InvalidOperationException(witness);
        }
        private static object Invoke(object instance, string method, params object[] arguments)
        {
            try { return instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Invoke(instance, arguments); }
            catch (TargetInvocationException error) when (error.InnerException != null)
            { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }
        private static FieldInfo BaseField(object instance, string name)
            => instance.GetType().BaseType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        private static List<TPair> Rows<TPair>(object instance)
            => (List<TPair>)BaseField(instance, "m_values").GetValue(instance);

        public static int RunOriginalMetadata()
        {
            checks = 0;
            Type[] types = { typeof(SerializableDictionary<,,>), typeof(SerializableDictionary<,,,>),
                typeof(SerializableDictionaryKvpList<,,,>), typeof(SerializableKeyValueListPair<,>) };
            for (int index = 0; index < types.Length; index++)
            {
                Type type = types[index];
                Check(type.Assembly.GetName().Name == "HLUnityCore.Runtime" && type.IsPublic && type.IsSerializable
                    && (type.Attributes & TypeAttributes.BeforeFieldInit) != 0, "original assembly and type flags");
                var options = type.GetCustomAttributes<Il2CppSetOptionAttribute>(false).ToArray();
                Option first = index == 1 ? Option.ArrayBoundsChecks : Option.NullChecks;
                Option second = index == 1 ? Option.NullChecks : Option.ArrayBoundsChecks;
                Check(options.Length == 2 && options[0].Option == first && options[1].Option == second
                    && options.All(option => option.Value is bool flag && !flag), "original ordered compiler options");
                var parameters = type.GetGenericArguments();
                Check(parameters.Select((parameter, parameterIndex) => parameter.GenericParameterAttributes
                    == (index == 0 && parameterIndex == 2 ? GenericParameterAttributes.None : GenericParameterAttributes.ReferenceTypeConstraint)
                    && parameter.GetGenericParameterConstraints().Length == 0).All(value => value), "original complete generic constraints");
                Check(type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length
                    + type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length
                    == (index == 3 ? 6 : 4), "complete original declaration counts");
            }
            var fields = types[3].GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .OrderBy(field => field.MetadataToken).ToArray();
            Check(fields.Length == 2 && fields[0].Name == "m_key" && fields[1].Name == "m_values"
                && fields.All(field => field.IsPrivate && !field.IsInitOnly && field.IsDefined(typeof(SerializeField), false)), "original mutable serialized pair fields");
            Check(types.Take(3).All(type => type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length == 0), "all three original dictionary owners fieldless");
            Check(types.Take(3).SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                .All(method => method.IsFamily && method.IsVirtual && !method.IsAbstract), "original protected override conversions");
            Check(types[3].GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly).Where(method => method.IsGenericMethod)
                .SelectMany(method => method.GetGenericArguments()).All(parameter => parameter.GenericParameterAttributes == GenericParameterAttributes.ReferenceTypeConstraint
                    && parameter.GetGenericParameterConstraints().Length == 0), "all four original method cast parameters reference constrained");
            return checks;
        }

        public static int RunReferenceConversions()
        {
            checks = 0;
            var defaultThree = new SerializableDictionary<string, object, int>();
            Check(defaultThree.Count == 0, "arity three default constructor");
            var three = new SerializableDictionary<string, object, int>(StringComparer.OrdinalIgnoreCase);
            Check(ReferenceEquals(((Dictionary<string, int>)BaseField(three, "m_dictionary").GetValue(three)).Comparer, StringComparer.OrdinalIgnoreCase), "arity three comparer identity");
            three.Add("A", -7); three["a"] = int.MinValue;
            Check(three.Count == 1 && three["A"] == int.MinValue, "comparer affects genuine backing dictionary");
            three.Serialize();
            var rowsThree = Rows<SerializableKeyValuePair<object, int>>(three);
            Check(rowsThree.Count == 1 && (string)rowsThree[0].Key == "A" && rowsThree[0].Value == int.MinValue, "unrestricted integer value serialized unchanged");
            three.Clear(); three.Deserialize();
            Check(three.Count == 1 && three["a"] == int.MinValue, "integer round trip through original conversions");
            var defaultFour = new SerializableDictionary<string, object, string, object>();
            Check(defaultFour.Count == 0, "arity four default constructor");
            var four = new SerializableDictionary<string, object, string, object>(StringComparer.OrdinalIgnoreCase);
            Check(ReferenceEquals(((Dictionary<string, string>)BaseField(four, "m_dictionary").GetValue(four)).Comparer, StringComparer.OrdinalIgnoreCase), "arity four comparer identity");
            string original = new string(new[] { 'v', 'a', 'l', 'u', 'e' });
            four.Add("B", original); four["b"] = original; four.Serialize();
            Check(four.Count == 1 && ReferenceEquals(Rows<SerializableKeyValuePair<object, object>>(four)[0].Value, original), "reference value serialization preserves identity");
            four.Clear(); four.Deserialize();
            Check(ReferenceEquals(four["b"], original), "reference value round trip preserves identity");
            var incompatibleThree = new SerializableDictionary<object, string, int>();
            var failedKey = (SerializableKeyValuePair<string, int>)Invoke(incompatibleThree, "SerializeKeyValuePair", new StringBuilder("key"), 31);
            Check(failedKey.Key == null && failedKey.Value == 31, "failed key conversion retains null and unchanged value");
            var nullKey = (SerializableKeyValuePair<string, int>)Invoke(incompatibleThree, "SerializeKeyValuePair", null, -1);
            Check(nullKey.Key == null && nullKey.Value == -1, "null key serialization retains value");
            var incompatibleFour = new SerializableDictionary<object, string, object, string>();
            var failedBoth = (SerializableKeyValuePair<string, string>)Invoke(incompatibleFour, "SerializeKeyValuePair", new object(), new object());
            Check(failedBoth.Key == null && failedBoth.Value == null, "both failed serialization casts remain null");
            var output = new object[] { new SerializableKeyValuePair<object, object>(123, new object()), "old", "old" };
            Invoke(defaultFour, "DeserializeKeyValuePair", output);
            Check(output[1] == null && output[2] == null, "both incompatible deserialization casts remain null");
            output = new object[] { new SerializableKeyValuePair<object, int>(new object(), 47), "old", 0 };
            Invoke(defaultThree, "DeserializeKeyValuePair", output);
            Check(output[1] == null && (int)output[2] == 47, "deserialization retains unrestricted value after key mismatch");
            return checks;
        }

        public static int RunListPairMutationAndCopies()
        {
            checks = 0;
            var pair = new SerializableKeyValueListPair<string, string>();
            Check(pair.Key == null && pair.Values != null && pair.Values.Count == 0, "list field initializes before use");
            var originalList = pair.Values;
            Check(ReferenceEquals(originalList, pair.Values), "Values returns live list identity");
            var source = new List<object> { "first", new StringBuilder("incompatible"), null, "last" };
            pair.Set<object, object>("key", source);
            Check(pair.Key == "key" && ReferenceEquals(pair.Values, originalList), "Set preserves destination identity and converts key");
            Check(pair.Values.SequenceEqual(new[] { "first", null, null, "last" }), "each failed or null value occupies its original position");
            source.Clear();
            Check(pair.Values.Count == 4, "Set copies rather than aliases input list");
            Check(pair.KeyAs<object>() is string && pair.KeyAs<StringBuilder>() == null, "KeyAs handles success and failure");
            var copied = pair.ValuesAs<object>();
            Check(!ReferenceEquals(copied, originalList) && copied.Count == 4 && (string)copied[0] == "first" && copied[1] == null, "ValuesAs creates a separate converted list");
            copied.Clear();
            Check(pair.Values.Count == 4, "modifying copied list leaves source untouched");
            var incompatible = pair.ValuesAs<StringBuilder>();
            Check(incompatible.Count == 4 && incompatible.All(value => value == null), "ValuesAs retains every failed cast as null");
            pair.Set("self", pair.Values);
            Check(pair.Key == "self" && pair.Values.Count == 0 && ReferenceEquals(originalList, pair.Values), "self alias clears before source enumeration");
            var emptyOne = pair.ValuesAs<object>(); var emptyTwo = pair.ValuesAs<object>();
            Check(emptyOne.Count == 0 && emptyTwo.Count == 0 && !ReferenceEquals(emptyOne, emptyTwo), "empty conversions each allocate");
            pair.Values.Add("old");
            Throws<NullReferenceException>(() => pair.Set<object, object>("published", null), "null input faults after mutation");
            Check(pair.Key == "published" && pair.Values.Count == 0, "null input preserves published key and cleared list");
            pair.Set<object, object>(new object(), new List<object> { "next" });
            Check(pair.Key == null && pair.Values.Single() == "next", "failed key does not suppress values");
            return checks;
        }

        public static int RunListDictionaryOrdering()
        {
            checks = 0;
            var dictionary = new SerializableDictionaryKvpList<string, object, string, object>();
            Check(dictionary.Count == 0, "list dictionary constructor");
            string[] array = { "first", null, "last" };
            dictionary.Set(new Dictionary<string, string[]> { ["key"] = array });
            Check(dictionary["key"].SequenceEqual(array) && !ReferenceEquals(dictionary["key"], array), "Set copies array preserving null positions");
            array[0] = "changed";
            Check(dictionary["key"][0] == "first", "array mutation cannot change copied list");
            var originalValues = dictionary["key"]; dictionary.Serialize();
            var rows = Rows<SerializableKeyValueListPair<object, object>>(dictionary);
            Check(rows.Count == 1 && (string)rows[0].Key == "key" && rows[0].Values.Count == 3 && !ReferenceEquals(rows[0].Values, originalValues), "serialization allocates independent pair list");
            dictionary.Clear(); dictionary.Deserialize();
            Check(dictionary["key"].SequenceEqual(new[] { "first", null, "last" }) && !ReferenceEquals(dictionary["key"], rows[0].Values), "deserialization allocates another converted list");
            dictionary["key"][0] = "after";
            Check((string)rows[0].Values[0] == "first", "deserialized list mutation preserves serialized rows");
            Throws<NullReferenceException>(() => dictionary.Set((Dictionary<string, string[]>)null), "null Set argument faults");
            Check(dictionary.Count == 0 && ReferenceEquals(rows, Rows<SerializableKeyValueListPair<object, object>>(dictionary)) && rows.Count == 1, "Set clears backing dictionary before null fault and retains serialized rows");
            var invalid = new Dictionary<string, string[]> { ["accepted"] = new[] { "value" }, ["fault"] = null };
            Throws<ArgumentNullException>(() => dictionary.Set(invalid), "later null array faults during list constructor");
            Check(dictionary.Count == 1 && dictionary["accepted"].Single() == "value" && !dictionary.ContainsKey("fault") && rows.Count == 1, "later fault preserves preceding additions and stale rows");
            dictionary.Set(new Dictionary<string, string[]>());
            Check(dictionary.Count == 0 && rows.Count == 1, "empty Set clears backing only");
            dictionary.Reset();
            Check(rows.Count == 0, "existing base Reset clears serialized rows");
            return checks;
        }
    }
}
