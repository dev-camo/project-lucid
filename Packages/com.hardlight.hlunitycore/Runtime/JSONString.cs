using System.Text;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.JSON
{
    // Original HLUnityCore.Runtime 0x0200029e, whole eight-method owner.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class JSONString : JSONObjectPooled<JSONString>
    {
        private string m_value; // 0x0400085f.
        public string Value
        {
            get { return m_value; } // 0x060010e2.
            set { m_value = value; } // 0x060010e3.
        }
        // 0x060010e4. Original Spawn before reading the source field.
        public override IJsonObject Clone()
        {
            JSONString result = Spawn();
            result.m_value = m_value;
            return result;
        }
        // 0x060010e5.
        public static JSONString Create(string value)
        {
            JSONString result = Spawn();
            result.m_value = value;
            return result;
        }
        // 0x060010e6. Original Spawn before ParseString, retaining object on parse failure.
        public static JSONString Create(char[] json, ref int index, ref bool success)
        {
            JSONString result = Spawn();
            result.m_value = JSONSerializer.ParseString(json, ref index, ref success);
            return result;
        }
        // 0x060010e7. Original literal "null" is passed through SerializeString;
        // its result is ignored, so a null stored string encodes as a quoted string.
        public override bool Encode(StringBuilder builder)
        {
            JSONSerializer.SerializeString(m_value ?? "null", builder);
            return true;
        }
        // 0x060010e8, original field reset; no base Reset call.
        protected override void Reset() { m_value = null; }
        // 0x060010e9, original pooled base constructor only.
        public JSONString() { }
    }
}
