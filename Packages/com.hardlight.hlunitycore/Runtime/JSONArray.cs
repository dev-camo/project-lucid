using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.JSON
{
    // Original HLUnityCore.Runtime 0x02000294; complete twenty-four-method owner.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class JSONArray : JSONObjectPooled<JSONArray>, IEnumerable<IJsonObject>, IEnumerable
    {
        // 0x04000844 / 0x06001040. Original capacity ten before the base constructor.
        private List<IJsonObject> m_value = new List<IJsonObject>(10);

        // 0x06001029..2a. Replacement does not release the old entry.
        public IJsonObject this[int key]
        {
            get { return m_value[key]; }
            set { m_value[key] = value; }
        }

        // 0x0600102b.
        public int Count { get { return m_value.Count; } }

        // 0x0600102c and 0x0600102d each box the list enumerator directly.
        public IEnumerator<IJsonObject> GetEnumerator() { return m_value.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return m_value.GetEnumerator(); }

        // 0x0600102e. Original live Count and second index read on a non-null entry.
        public override IJsonObject Clone()
        {
            JSONArray result = Spawn();
            for (int i = 0; i < m_value.Count; i++)
                result.Add(m_value[i] != null ? m_value[i].Clone() : null);
            return result;
        }

        // 0x0600102f. Spawn precedes enumeration; original null-source fault retained.
        public static JSONArray Create(IEnumerable<float> array)
        {
            JSONArray result = Spawn();
            foreach (float value in array) result.m_value.Add(JSONDouble.Create(value));
            return result;
        }

        // 0x06001030. Spawn precedes enumeration; original null-source fault retained.
        public static JSONArray Create(IEnumerable<double> array)
        {
            JSONArray result = Spawn();
            foreach (double value in array) result.m_value.Add(JSONDouble.Create(value));
            return result;
        }

        // 0x06001031. Spawn precedes enumeration; original null-source fault retained.
        public static JSONArray Create(IEnumerable<int> array)
        {
            JSONArray result = Spawn();
            foreach (int value in array) result.m_value.Add(JSONLong.Create(value));
            return result;
        }

        // 0x06001032. Spawn precedes enumeration; original null-source fault retained.
        public static JSONArray Create(IEnumerable<long> array)
        {
            JSONArray result = Spawn();
            foreach (long value in array) result.m_value.Add(JSONLong.Create(value));
            return result;
        }

        // 0x06001033. Spawn precedes enumeration; original null-source fault retained.
        public static JSONArray Create(IEnumerable<string> array)
        {
            JSONArray result = Spawn();
            foreach (string value in array) result.m_value.Add(JSONString.Create(value));
            return result;
        }

        // 0x06001034. Spawn precedes enumeration; original null-source fault retained.
        public static JSONArray Create(IEnumerable<bool> array)
        {
            JSONArray result = Spawn();
            foreach (bool value in array) result.m_value.Add(JSONBool.Create(value));
            return result;
        }

        // 0x06001035. Original permissive comma loop and partial pooled-object retention
        // on a malformed token/value; the initial NextToken does not validate an opener.
        public static JSONArray Create(char[] json, ref int index, ref bool success)
        {
            JSONArray result = Spawn();
            JSONSerializer.NextToken(json, ref index);
            while (true)
            {
                int token = JSONSerializer.LookAhead(json, index);
                if (token == JSONSerializer.TOKEN_COMMA)
                {
                    JSONSerializer.NextToken(json, ref index);
                    continue;
                }
                if (token == JSONSerializer.TOKEN_SQUARED_CLOSE)
                {
                    JSONSerializer.NextToken(json, ref index);
                    return result;
                }
                if (token == JSONSerializer.TOKEN_NONE)
                {
                    success = false;
                    return null;
                }
                IJsonObject value = JSONSerializer.ParseValue(json, ref index, ref success);
                if (!success) return null;
                result.Add(value);
            }
        }

        // 0x06001036. Original upper-bound-only check: negative indices still fault.
        public bool TryGetFloatFromIndex(int index, out float value)
        {
            value = 0;
            if (m_value.Count > index) return JSONHelper.TryConvertToType(m_value[index], out value);
            return false;
        }

        // 0x06001037. Original upper-bound-only check: negative indices still fault.
        public bool TryGetDoubleFromIndex(int index, out double value)
        {
            value = 0;
            if (m_value.Count > index) return JSONHelper.TryConvertToType(m_value[index], out value);
            return false;
        }

        // 0x06001038. Original upper-bound-only check: negative indices still fault.
        public bool TryGetIntFromIndex(int index, out int value)
        {
            value = 0;
            if (m_value.Count > index) return JSONHelper.TryConvertToType(m_value[index], out value);
            return false;
        }

        // 0x06001039. Original upper-bound-only check: negative indices still fault.
        public bool TryGetLongFromIndex(int index, out long value)
        {
            value = 0;
            if (m_value.Count > index) return JSONHelper.TryConvertToType(m_value[index], out value);
            return false;
        }

        // 0x0600103a. Original upper-bound-only check: negative indices still fault.
        public bool TryGetStringFromIndex(int index, out string value)
        {
            value = null;
            if (m_value.Count > index) return JSONHelper.TryConvertToType(m_value[index], out value);
            return false;
        }

        // 0x0600103b. Original upper-bound-only check: negative indices still fault.
        public bool TryGetBoolFromIndex(int index, out bool value)
        {
            value = false;
            if (m_value.Count > index) return JSONHelper.TryConvertToType(m_value[index], out value);
            return false;
        }

        // 0x0600103c.
        public void Add(IJsonObject encodable) { m_value.Add(encodable); }

        // 0x0600103d. Original Public-only reflection flags and ignored serializer result.
        public override bool Encode(StringBuilder builder)
        {
            JSONSerializer.SerializeArray(m_value, builder, BindingFlags.Public, JSONSerializer.EncodeOptions.None);
            return true;
        }

        // 0x0600103e. Original pool return occurs BEFORE child releases. Count is live;
        // null children fault, and child callbacks can observe/reenter the returned parent.
        public override void Release()
        {
            base.Release();
            for (int i = 0; i < m_value.Count; i++) m_value[i].Release();
        }

        // 0x0600103f. No additional child release or base Reset is introduced.
        protected override void Reset() { m_value.Clear(); }

        // 0x06001040. List initialization above preserves original constructor order.
        public JSONArray() { }
    }
}
