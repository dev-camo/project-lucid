using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Hardlight.JSON;
using Hardlight.Networking;
using Hardlight.Pooling;

namespace ProjectLucid.Verification
{
    // Verification code, separate from preserved original runtime implementation.
    public static class JsonHttpPreservationVerification
    {
        private const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.NonPublic;
        private static readonly Type[] PooledTypes =
        {
            typeof(JSONArray), typeof(JSONBool), typeof(JSONDictionary), typeof(JSONDouble),
            typeof(JSONHashtable), typeof(JSONLong), typeof(JSONString)
        };

        // Use fresh pool references for a fixture. Restoring the prior reference
        // leaves its existing free/used objects, ordering and contents untouched,
        // including when an original malformed parse intentionally leaks a value.
        private sealed class IsolatedState : IDisposable
        {
            private readonly List<KeyValuePair<FieldInfo, object>> fields = new List<KeyValuePair<FieldInfo, object>>();
            private readonly CultureInfo culture = CultureInfo.CurrentCulture;
            public IsolatedState()
            {
                foreach (Type type in PooledTypes)
                {
                    Replace(typeof(ObjectPool<>).MakeGenericType(type), "s_objectPool", null);
                    Replace(typeof(JSONObjectPooled<>).MakeGenericType(type), "s_poolInitialised", false);
                }
                Replace(typeof(JSONSerializer), "s_tempStringBuilder", new StringBuilder(10240));
                Replace(typeof(JSONSerializer), "s_derivedTypes", new Dictionary<Type, List<Type>>());
                Replace(typeof(OriginalJsonManagedVerification), "checks", 0);
                Replace(typeof(HTTPRequestBehaviour), "<LastUri>k__BackingField", null);
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            }
            private void Replace(Type owner, string name, object replacement)
            {
                FieldInfo field = owner.GetField(name, StaticFields);
                if (field == null) throw new MissingFieldException(owner.FullName, name);
                fields.Add(new KeyValuePair<FieldInfo, object>(field, field.GetValue(null)));
                field.SetValue(null, replacement);
            }
            public void Dispose()
            {
                for (int i = fields.Count - 1; i >= 0; --i)
                    fields[i].Key.SetValue(null, fields[i].Value);
                CultureInfo.CurrentCulture = culture;
            }
        }

        public static int ScalarConversions()
        {
            using (new IsolatedState()) return OriginalJsonManagedVerification.RunScalarConversions();
        }
        public static int ParserAndCursor()
        {
            using (new IsolatedState()) return OriginalJsonManagedVerification.RunParserAndCursor();
        }
        public static int SerializationOrder()
        {
            using (new IsolatedState()) return OriginalJsonManagedVerification.RunSerializationOrder();
        }
        public static int ContainerOwnership()
        {
            using (new IsolatedState()) return OriginalJsonManagedVerification.RunContainerOwnership();
        }
        public static int HttpNullRequestBoundaries()
        {
            using (new IsolatedState()) return OriginalHttpManagedVerification.RunNativeFreeBoundaries();
        }

        public static int ExistingStateSurvivesRepeatedChecksAndFaults()
        {
            int checks = 0;
            using (new IsolatedState())
            {
                // Seed genuine preexisting pooled data and parser cache, then run
                // inner fixtures twice. Inner fixtures must restore these identities.
                JSONArray array = JSONArray.Create(new int[] { 41 });
                JSONHashtable table = JSONHashtable.Create(); table.Add("seed", 73L);
                FieldInfo builderField = typeof(JSONSerializer).GetField("s_tempStringBuilder", StaticFields);
                var builder = (StringBuilder)builderField.GetValue(null); builder.Append("seed text");
                FieldInfo derivedField = typeof(JSONSerializer).GetField("s_derivedTypes", StaticFields);
                var derived = (Dictionary<Type, List<Type>>)derivedField.GetValue(null);
                var row = new List<Type> { typeof(JSONLong) }; derived.Add(typeof(IJsonObject), row);
                FieldInfo uriField = typeof(HTTPRequestBehaviour).GetField("<LastUri>k__BackingField", StaticFields);
                var uri = new Uri("https://example.invalid/preserved-fixture-state"); uriField.SetValue(null, uri);
                FieldInfo poolField = typeof(ObjectPool<JSONArray>).GetField("s_objectPool", StaticFields);
                object pool = poolField.GetValue(null);
                int used = ObjectPool<JSONArray>.UsedObjectCount;
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                CultureInfo seededCulture = CultureInfo.CurrentCulture;
                for (int repeat = 0; repeat < 2; ++repeat)
                {
                    int total = ScalarConversions() + ParserAndCursor() + SerializationOrder() + ContainerOwnership();
                    Require(total == 104 && HttpNullRequestBoundaries() == 21, ref checks);
                    Require(ReferenceEquals(pool, poolField.GetValue(null)) && ObjectPool<JSONArray>.UsedObjectCount == used, ref checks);
                    Require(array.Count == 1 && array.TryGetIntFromIndex(0, out int seedValue) && seedValue == 41 && table.GetLong("seed") == 73, ref checks);
                    Require(ReferenceEquals(builder, builderField.GetValue(null)) && builder.ToString() == "seed text", ref checks);
                    Require(ReferenceEquals(derived, derivedField.GetValue(null)) && ReferenceEquals(row, derived[typeof(IJsonObject)]), ref checks);
                    Require(ReferenceEquals(uri, uriField.GetValue(null)) && ReferenceEquals(seededCulture, CultureInfo.CurrentCulture), ref checks);
                }
                try
                {
                    using (new IsolatedState())
                    {
                        JSONSerializer.Decode("{\"a\":1,\"a\":2}");
                        throw new InvalidOperationException("Original duplicate-key parse must fault.");
                    }
                }
                catch (ArgumentException) { ++checks; }
                Require(ReferenceEquals(pool, poolField.GetValue(null)) && ObjectPool<JSONArray>.UsedObjectCount == used, ref checks);
                Require(ReferenceEquals(builder, builderField.GetValue(null)) && builder.ToString() == "seed text", ref checks);
                Require(ReferenceEquals(derived, derivedField.GetValue(null)) && ReferenceEquals(row, derived[typeof(IJsonObject)]), ref checks);
                Require(ReferenceEquals(uri, uriField.GetValue(null)) && ReferenceEquals(seededCulture, CultureInfo.CurrentCulture), ref checks);
            }
            return checks;
        }
        private static void Require(bool value, ref int count)
        {
            if (!value) throw new InvalidOperationException("Preservation fixture changed the caller's state.");
            ++count;
        }
    }
}
