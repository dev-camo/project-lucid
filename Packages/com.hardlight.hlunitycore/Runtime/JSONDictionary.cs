using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.JSON
{
    // Original HLUnityCore.Runtime 0x02000296; complete forty-five-method owner.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class JSONDictionary : JSONObjectPooled<JSONDictionary>, IDisposable,
        IEnumerable<KeyValuePair<IJsonObject, IJsonObject>>, IEnumerable
    {
        // 0x04000846/0x04000847. Original dictionary format marker.
        public const string TokenDictionaryKey = "TokenDictionaryKey";
        public const string TokenDictionaryValue = "TokenDictionaryValue";

        // 0x04000848 / 0x06001074. Original capacity thirty, initialized before base.
        private Dictionary<IJsonObject, IJsonObject> m_container = new Dictionary<IJsonObject, IJsonObject>(30);

        // 0x06001048. Live keys collection, with no copied snapshot.
        public Dictionary<IJsonObject, IJsonObject>.KeyCollection Keys { get { return m_container.Keys; } }

        // 0x06001049. Original Add order and null-preserving value clone.
        public override IJsonObject Clone()
        {
            JSONDictionary result = Spawn();
            foreach (KeyValuePair<IJsonObject, IJsonObject> pair in m_container)
            {
                IJsonObject cloned = pair.Value != null ? pair.Value.Clone() : null;
                result.m_container.Add(pair.Key, cloned);
            }
            return result;
        }

        // 0x0600104a.
        public static JSONDictionary Create() { return Spawn(); }

        // 0x0600104b. Original lookahead restores the cursor only on normal return.
        // success is intentionally passed by value; exceptions keep the advanced cursor.
        public static bool CheckType(char[] json, ref int index, bool success)
        {
            int originalIndex = index;
            JSONSerializer.NextToken(json, ref index);
            string key = JSONSerializer.ParseString(json, ref index, ref success);
            bool result;
            if (key != TokenDictionaryKey || !success) result = false;
            else if (JSONSerializer.NextToken(json, ref index) != JSONSerializer.TOKEN_COLON) result = false;
            else
            {
                string value = JSONSerializer.ParseString(json, ref index, ref success);
                result = value == TokenDictionaryValue && success;
            }
            index = originalIndex;
            return result;
        }

        // 0x0600104c. Original permissive comma handling and pooled partial retention.
        // A missing colon returns null without additionally clearing success.
        public static JSONDictionary Create(char[] json, ref int index, ref bool success)
        {
            JSONDictionary result = Spawn();
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
                IJsonObject key = JSONSerializer.ParseValue(json, ref index, ref success);
                if (!success) return null;
                if (JSONSerializer.NextToken(json, ref index) != JSONSerializer.TOKEN_COLON) return null;
                IJsonObject value = JSONSerializer.ParseValue(json, ref index, ref success);
                if (!success) return null;
                result.m_container.Add(key, value);
            }
        }

        // 0x0600104d. Original header always precedes entries. Keys use ToString,
        // not Encode/escaping; every entry has a comma after the format marker.
        // A failed child leaves the partial builder without its closing brace.
        public override bool Encode(StringBuilder builder)
        {
            builder.Append("{");
            builder.Append("\"");
            builder.Append(TokenDictionaryKey);
            builder.Append("\"");
            builder.Append(":");
            builder.Append("\"");
            builder.Append(TokenDictionaryValue);
            builder.Append("\"");
            foreach (KeyValuePair<IJsonObject, IJsonObject> pair in m_container)
            {
                string key = pair.Key.ToString();
                builder.Append(", ");
                builder.Append("\"");
                builder.Append(key);
                builder.Append("\"");
                builder.Append(":");
                if (pair.Value != null)
                {
                    if (!pair.Value.Encode(builder)) return false;
                }
                else builder.Append("null");
            }
            builder.Append("}");
            return true;
        }

        // 0x0600104e. Original virtual dispatch rather than a direct base release.
        public void Dispose() { Release(); }

        // 0x0600104f. Original key release precedes value release in every pair.
        // Aliased key/value objects can release twice; child faults precede Clear/base.
        public override void Release()
        {
            foreach (KeyValuePair<IJsonObject, IJsonObject> pair in m_container)
            {
                if (pair.Key != null) pair.Key.Release();
                if (pair.Value != null) pair.Value.Release();
            }
            m_container.Clear();
            base.Release();
        }

        // 0x06001050.
        protected override void Reset() { m_container.Clear(); }

        // 0x06001051. Original Dictionary.Add duplicate/null-key faults.
        public void Add(IJsonObject key, IJsonObject value) { m_container.Add(key, value); }

        // 0x06001052. Original typed reuse; wrong-type casts and null-entry duplicate Add still fault.
        public void Add(IJsonObject key, long value)
        {
            IJsonObject existing = Get(key);
            if (existing == null) m_container.Add(key, JSONLong.Create(value));
            else ((JSONLong)existing).Value = value;
        }

        // 0x06001053. Original widening and forwarding.
        public void Add(IJsonObject key, uint value) { Add(key, (long)value); }

        // 0x06001054. Original widening and forwarding.
        public void Add(IJsonObject key, int value) { Add(key, (long)value); }

        // 0x06001055. Original typed reuse; wrong-type casts and null-entry duplicate Add still fault.
        public void Add(IJsonObject key, string value)
        {
            IJsonObject existing = Get(key);
            if (existing == null) m_container.Add(key, JSONString.Create(value));
            else ((JSONString)existing).Value = value;
        }

        // 0x06001056. Original widening and forwarding.
        public void Add(IJsonObject key, float value) { Add(key, (double)value); }

        // 0x06001057. Original typed reuse; wrong-type casts and null-entry duplicate Add still fault.
        public void Add(IJsonObject key, bool value)
        {
            IJsonObject existing = Get(key);
            if (existing == null) m_container.Add(key, JSONBool.Create(value));
            else ((JSONBool)existing).Value = value;
        }

        // 0x06001058. Original typed reuse; wrong-type casts and null-entry duplicate Add still fault.
        public void Add(IJsonObject key, double value)
        {
            IJsonObject existing = Get(key);
            if (existing == null) m_container.Add(key, JSONDouble.Create(value));
            else ((JSONDouble)existing).Value = value;
        }

        // 0x06001059. Original direct Add, with no replacement or release.
        public void Add(IJsonObject key, JSONHashtable value) { m_container.Add(key, value); }

        // 0x0600105a. Release and Remove precede enumeration/allocation of the new array.
        public void Add(IJsonObject key, IEnumerable<float> array)
        {
            IJsonObject existing = Get(key);
            if (existing != null)
            {
                ((JSONArray)existing).Release();
                m_container.Remove(key);
            }
            m_container.Add(key, JSONArray.Create(array));
        }

        // 0x0600105b. Release and Remove precede enumeration/allocation of the new array.
        public void Add(IJsonObject key, IEnumerable<double> array)
        {
            IJsonObject existing = Get(key);
            if (existing != null)
            {
                ((JSONArray)existing).Release();
                m_container.Remove(key);
            }
            m_container.Add(key, JSONArray.Create(array));
        }

        // 0x0600105c. Release and Remove precede enumeration/allocation of the new array.
        public void Add(IJsonObject key, IEnumerable<int> array)
        {
            IJsonObject existing = Get(key);
            if (existing != null)
            {
                ((JSONArray)existing).Release();
                m_container.Remove(key);
            }
            m_container.Add(key, JSONArray.Create(array));
        }

        // 0x0600105d. Release and Remove precede enumeration/allocation of the new array.
        public void Add(IJsonObject key, IEnumerable<long> array)
        {
            IJsonObject existing = Get(key);
            if (existing != null)
            {
                ((JSONArray)existing).Release();
                m_container.Remove(key);
            }
            m_container.Add(key, JSONArray.Create(array));
        }

        // 0x0600105e. Release and Remove precede enumeration/allocation of the new array.
        public void Add(IJsonObject key, IEnumerable<string> array)
        {
            IJsonObject existing = Get(key);
            if (existing != null)
            {
                ((JSONArray)existing).Release();
                m_container.Remove(key);
            }
            m_container.Add(key, JSONArray.Create(array));
        }

        // 0x0600105f. Release and Remove precede enumeration/allocation of the new array.
        public void Add(IJsonObject key, IEnumerable<bool> array)
        {
            IJsonObject existing = Get(key);
            if (existing != null)
            {
                ((JSONArray)existing).Release();
                m_container.Remove(key);
            }
            m_container.Add(key, JSONArray.Create(array));
        }

        // 0x06001060.
        public IJsonObject Get(IJsonObject key)
        {
            IJsonObject value;
            return m_container.TryGetValue(key, out value) ? value : null;
        }

        // 0x06001061. Original optional default and helper conversion.
        public string GetString(IJsonObject key, string defaultIfFail = null)
        {
            return JSONHelper.ConvertToType(Get(key), defaultIfFail);
        }

        // 0x06001062. Original optional default and helper conversion.
        public int GetInt(IJsonObject key, int defaultIfFail = 0)
        {
            return JSONHelper.ConvertToType(Get(key), defaultIfFail);
        }

        // 0x06001063. Original optional default and helper conversion.
        public long GetLong(IJsonObject key, long defaultIfFail = 0)
        {
            return JSONHelper.ConvertToType(Get(key), defaultIfFail);
        }

        // 0x06001064. Original optional default and helper conversion.
        public float GetFloat(IJsonObject key, float defaultIfFail = 0)
        {
            return JSONHelper.ConvertToType(Get(key), defaultIfFail);
        }

        // 0x06001065. Original optional default and helper conversion.
        public bool GetBool(IJsonObject key, bool defaultIfFail = false)
        {
            return JSONHelper.ConvertToType(Get(key), defaultIfFail);
        }

        // 0x06001066. Original optional default and helper conversion.
        public double GetDouble(IJsonObject key, double defaultIfFail = 0)
        {
            return JSONHelper.ConvertToType(Get(key), defaultIfFail);
        }

        // 0x06001067. Original non-throwing type test.
        public JSONArray GetArray(IJsonObject key) { return Get(key) as JSONArray; }

        // 0x06001068. Original non-throwing type test.
        public JSONHashtable GetObject(IJsonObject key) { return Get(key) as JSONHashtable; }

        // 0x06001069. Get/key lookup precedes helper out initialization.
        public bool TryGetString(IJsonObject key, out string value)
        {
            return JSONHelper.TryConvertToType(Get(key), out value);
        }

        // 0x0600106a. Get/key lookup precedes helper out initialization.
        public bool TryGetInt(IJsonObject key, out int value)
        {
            return JSONHelper.TryConvertToType(Get(key), out value);
        }

        // 0x0600106b. Get/key lookup precedes helper out initialization.
        public bool TryGetLong(IJsonObject key, out long value)
        {
            return JSONHelper.TryConvertToType(Get(key), out value);
        }

        // 0x0600106c. Get/key lookup precedes helper out initialization.
        public bool TryGetFloat(IJsonObject key, out float value)
        {
            return JSONHelper.TryConvertToType(Get(key), out value);
        }

        // 0x0600106d. Get/key lookup precedes helper out initialization.
        public bool TryGetBool(IJsonObject key, out bool value)
        {
            return JSONHelper.TryConvertToType(Get(key), out value);
        }

        // 0x0600106e. Get/key lookup precedes helper out initialization.
        public bool TryGetDouble(IJsonObject key, out double value)
        {
            return JSONHelper.TryConvertToType(Get(key), out value);
        }

        // 0x0600106f. Original is-test followed by typed assignment, else null/false.
        public bool TryGetArray(IJsonObject key, out JSONArray value)
        {
            IJsonObject candidate = Get(key);
            if (candidate is JSONArray) { value = (JSONArray)candidate; return true; }
            value = null;
            return false;
        }

        // 0x06001070. Original is-test followed by typed assignment, else null/false.
        public bool TryGetObject(IJsonObject key, out JSONHashtable value)
        {
            IJsonObject candidate = Get(key);
            if (candidate is JSONHashtable) { value = (JSONHashtable)candidate; return true; }
            value = null;
            return false;
        }

        // 0x06001071.
        public bool ContainsKey(IJsonObject key) { return m_container.ContainsKey(key); }

        // 0x06001072 and a6 each box the genuine dictionary enumerator directly.
        public IEnumerator<KeyValuePair<IJsonObject, IJsonObject>> GetEnumerator() { return m_container.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return m_container.GetEnumerator(); }

        // 0x06001074. Field initializer above retains the original order.
        public JSONDictionary() { }
    }
}
