using System.Text;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.JSON
{
    // Original HLUnityCore.Runtime 0x02000295, whole seven-method owner.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class JSONBool : JSONObjectPooled<JSONBool>
    {
        private bool m_value; // 0x04000845.
        public bool Value
        {
            get { return m_value; } // 0x06001041.
            set { m_value = value; } // 0x06001042.
        }
        // 0x06001043. Spawn occurs before the source field is read.
        public override IJsonObject Clone()
        {
            JSONBool result = Spawn();
            result.m_value = m_value;
            return result;
        }
        // 0x06001044.
        public static JSONBool Create(bool value)
        {
            JSONBool result = Spawn();
            result.m_value = value;
            return result;
        }
        // 0x06001045, original lowercase JSON literals.
        public override bool Encode(StringBuilder builder)
        {
            builder.Append(m_value ? "true" : "false");
            return true;
        }
        // 0x06001046, original field reset; no base Reset call.
        protected override void Reset() { m_value = false; }
        // 0x06001047, original pooled base constructor only.
        public JSONBool() { }
    }
}
