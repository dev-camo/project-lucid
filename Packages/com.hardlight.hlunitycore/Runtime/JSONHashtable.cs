using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.JSON
{
    // Original HLUnityCore.Runtime 0x02000298; complete forty-four-method owner.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class JSONHashtable : JSONObjectPooled<JSONHashtable>, IDisposable,
        IEnumerable<KeyValuePair<string, IJsonObject>>, IEnumerable
    {
        // 0x0400084a / 0x060010a7. Original capacity thirty, initialized before base.
        private Dictionary<string, IJsonObject> m_container = new Dictionary<string, IJsonObject>(30);

        // 0x0600107c. Live keys collection, with no copied snapshot.
        public Dictionary<string, IJsonObject>.KeyCollection Keys { get { return m_container.Keys; } }

        // 0x0600107d. Original Add order and null-preserving value clone.
        public override IJsonObject Clone()
        {
            JSONHashtable result = Spawn();
            foreach (KeyValuePair<string, IJsonObject> pair in m_container)
            {
                IJsonObject cloned = pair.Value != null ? pair.Value.Clone() : null;
                result.m_container.Add(pair.Key, cloned);
            }
            return result;
        }

        // 0x0600107e.
        public static JSONHashtable Create() { return Spawn(); }

        // 0x0600107f. Original permissive comma handling and pooled partial retention.
        // A missing colon returns null without additionally clearing success.
        public static JSONHashtable Create(char[] json, ref int index, ref bool success)
        {
            JSONHashtable result = Spawn();
            JSONSerializer.NextToken(json, ref index);
            while (true)
            {
                int token = JSONSerializer.LookAhead(json, index);
                if (token == JSONSerializer.TOKEN_COMMA)
                {
                    JSONSerializer.NextToken(json, ref index);
                    continue;
                }
                if (token == JSONSerializer.TOKEN_NONE)
                {
                    success = false;
                    return null;
                }
                if (token == JSONSerializer.TOKEN_CURLY_CLOSE)
                {
                    JSONSerializer.NextToken(json, ref index);
                    return result;
                }
                string key = JSONSerializer.ParseString(json, ref index, ref success);
                if (!success) return null;
                if (JSONSerializer.NextToken(json, ref index) != JSONSerializer.TOKEN_COLON) return null;
                IJsonObject value = JSONSerializer.ParseValue(json, ref index, ref success);
                if (!success) return null;
                result.m_container.Add(key, value);
            }
        }

        // 0x06001080. Original keys use ToString before comma/quotes and are not escaped.
        // A failed child Encode leaves the partial builder without its closing brace.
        public override bool Encode(StringBuilder builder)
        {
            builder.Append("{");
            bool first = true;
            foreach (KeyValuePair<string, IJsonObject> pair in m_container)
            {
                string key = pair.Key.ToString();
                if (!first) builder.Append(", ");
                builder.Append("\"");
                builder.Append(key);
                builder.Append("\"");
                builder.Append(":");
                if (pair.Value != null)
                {
                    if (!pair.Value.Encode(builder)) return false;
                }
                else builder.Append("null");
                first = false;
            }
            builder.Append("}");
            return true;
        }

        // 0x06001081. Original virtual dispatch rather than a direct base release.
        public void Dispose() { Release(); }

        // 0x06001082. Non-null values release before Clear and base pool return.
        // Original foreach mutation/child fault behavior is retained.
        public override void Release()
        {
            foreach (KeyValuePair<string, IJsonObject> pair in m_container)
                if (pair.Value != null) pair.Value.Release();
            m_container.Clear();
            base.Release();
        }

        // 0x06001083.
        protected override void Reset() { m_container.Clear(); }

        // 0x06001084. Original Dictionary.Add duplicate/null-key faults.
        public void Add(string key, IJsonObject value) { m_container.Add(key, value); }

        // 0x06001085. Original typed reuse; wrong-type casts and null-entry duplicate Add still fault.
        public void Add(string key, long value)
        {
            IJsonObject existing = Get(key);
            if (existing == null) m_container.Add(key, JSONLong.Create(value));
            else ((JSONLong)existing).Value = value;
        }

        // 0x06001086. Original widening and forwarding.
        public void Add(string key, uint value) { Add(key, (long)value); }

        // 0x06001087. Original widening and forwarding.
        public void Add(string key, int value) { Add(key, (long)value); }

        // 0x06001088. Original typed reuse; wrong-type casts and null-entry duplicate Add still fault.
        public void Add(string key, string value)
        {
            IJsonObject existing = Get(key);
            if (existing == null) m_container.Add(key, JSONString.Create(value));
            else ((JSONString)existing).Value = value;
        }

        // 0x06001089. Original widening and forwarding.
        public void Add(string key, float value) { Add(key, (double)value); }

        // 0x0600108a. Original typed reuse; wrong-type casts and null-entry duplicate Add still fault.
        public void Add(string key, bool value)
        {
            IJsonObject existing = Get(key);
            if (existing == null) m_container.Add(key, JSONBool.Create(value));
            else ((JSONBool)existing).Value = value;
        }

        // 0x0600108b. Original typed reuse; wrong-type casts and null-entry duplicate Add still fault.
        public void Add(string key, double value)
        {
            IJsonObject existing = Get(key);
            if (existing == null) m_container.Add(key, JSONDouble.Create(value));
            else ((JSONDouble)existing).Value = value;
        }

        // 0x0600108c. Original direct Add, with no replacement or release.
        public void Add(string key, JSONHashtable value) { m_container.Add(key, value); }

        // 0x0600108d. Release and Remove precede enumeration/allocation of the new array.
        public void Add(string key, IEnumerable<float> array)
        {
            IJsonObject existing = Get(key);
            if (existing != null)
            {
                ((JSONArray)existing).Release();
                m_container.Remove(key);
            }
            m_container.Add(key, JSONArray.Create(array));
        }

        // 0x0600108e. Release and Remove precede enumeration/allocation of the new array.
        public void Add(string key, IEnumerable<double> array)
        {
            IJsonObject existing = Get(key);
            if (existing != null)
            {
                ((JSONArray)existing).Release();
                m_container.Remove(key);
            }
            m_container.Add(key, JSONArray.Create(array));
        }

        // 0x0600108f. Release and Remove precede enumeration/allocation of the new array.
        public void Add(string key, IEnumerable<int> array)
        {
            IJsonObject existing = Get(key);
            if (existing != null)
            {
                ((JSONArray)existing).Release();
                m_container.Remove(key);
            }
            m_container.Add(key, JSONArray.Create(array));
        }

        // 0x06001090. Release and Remove precede enumeration/allocation of the new array.
        public void Add(string key, IEnumerable<long> array)
        {
            IJsonObject existing = Get(key);
            if (existing != null)
            {
                ((JSONArray)existing).Release();
                m_container.Remove(key);
            }
            m_container.Add(key, JSONArray.Create(array));
        }

        // 0x06001091. Release and Remove precede enumeration/allocation of the new array.
        public void Add(string key, IEnumerable<string> array)
        {
            IJsonObject existing = Get(key);
            if (existing != null)
            {
                ((JSONArray)existing).Release();
                m_container.Remove(key);
            }
            m_container.Add(key, JSONArray.Create(array));
        }

        // 0x06001092. Release and Remove precede enumeration/allocation of the new array.
        public void Add(string key, IEnumerable<bool> array)
        {
            IJsonObject existing = Get(key);
            if (existing != null)
            {
                ((JSONArray)existing).Release();
                m_container.Remove(key);
            }
            m_container.Add(key, JSONArray.Create(array));
        }

        // 0x06001093.
        public IJsonObject Get(string key)
        {
            IJsonObject value;
            return m_container.TryGetValue(key, out value) ? value : null;
        }

        // 0x06001094. Original optional default and helper conversion.
        public string GetString(string key, string defaultIfFail = null)
        {
            return JSONHelper.ConvertToType(Get(key), defaultIfFail);
        }

        // 0x06001095. Original optional default and helper conversion.
        public int GetInt(string key, int defaultIfFail = 0)
        {
            return JSONHelper.ConvertToType(Get(key), defaultIfFail);
        }

        // 0x06001096. Original optional default and helper conversion.
        public long GetLong(string key, long defaultIfFail = 0)
        {
            return JSONHelper.ConvertToType(Get(key), defaultIfFail);
        }

        // 0x06001097. Original optional default and helper conversion.
        public float GetFloat(string key, float defaultIfFail = 0)
        {
            return JSONHelper.ConvertToType(Get(key), defaultIfFail);
        }

        // 0x06001098. Original optional default and helper conversion.
        public bool GetBool(string key, bool defaultIfFail = false)
        {
            return JSONHelper.ConvertToType(Get(key), defaultIfFail);
        }

        // 0x06001099. Original optional default and helper conversion.
        public double GetDouble(string key, double defaultIfFail = 0)
        {
            return JSONHelper.ConvertToType(Get(key), defaultIfFail);
        }

        // 0x0600109a. Original non-throwing type test.
        public JSONArray GetArray(string key) { return Get(key) as JSONArray; }

        // 0x0600109b. Original non-throwing type test.
        public JSONHashtable GetObject(string key) { return Get(key) as JSONHashtable; }

        // 0x0600109c. Get/key lookup precedes helper out initialization.
        public bool TryGetString(string key, out string value)
        {
            return JSONHelper.TryConvertToType(Get(key), out value);
        }

        // 0x0600109d. Get/key lookup precedes helper out initialization.
        public bool TryGetInt(string key, out int value)
        {
            return JSONHelper.TryConvertToType(Get(key), out value);
        }

        // 0x0600109e. Get/key lookup precedes helper out initialization.
        public bool TryGetLong(string key, out long value)
        {
            return JSONHelper.TryConvertToType(Get(key), out value);
        }

        // 0x0600109f. Get/key lookup precedes helper out initialization.
        public bool TryGetFloat(string key, out float value)
        {
            return JSONHelper.TryConvertToType(Get(key), out value);
        }

        // 0x060010a0. Get/key lookup precedes helper out initialization.
        public bool TryGetBool(string key, out bool value)
        {
            return JSONHelper.TryConvertToType(Get(key), out value);
        }

        // 0x060010a1. Get/key lookup precedes helper out initialization.
        public bool TryGetDouble(string key, out double value)
        {
            return JSONHelper.TryConvertToType(Get(key), out value);
        }

        // 0x060010a2. Original is-test followed by typed assignment, else null/false.
        public bool TryGetArray(string key, out JSONArray value)
        {
            IJsonObject candidate = Get(key);
            if (candidate is JSONArray) { value = (JSONArray)candidate; return true; }
            value = null;
            return false;
        }

        // 0x060010a3. Original is-test followed by typed assignment, else null/false.
        public bool TryGetObject(string key, out JSONHashtable value)
        {
            IJsonObject candidate = Get(key);
            if (candidate is JSONHashtable) { value = (JSONHashtable)candidate; return true; }
            value = null;
            return false;
        }

        // 0x060010a4.
        public bool ContainsKey(string key) { return m_container.ContainsKey(key); }

        // 0x060010a5 and a6 each box the genuine dictionary enumerator directly.
        public IEnumerator<KeyValuePair<string, IJsonObject>> GetEnumerator() { return m_container.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return m_container.GetEnumerator(); }

        // 0x060010a7. Field initializer above retains the original order.
        public JSONHashtable() { }
    }
}
