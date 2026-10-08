using System.Text;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.JSON
{
    // Original HLUnityCore.Runtime 0x0200029a, whole eight-method owner.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class JSONLong : JSONObjectPooled<JSONLong>
    {
        protected long m_value; // 0x0400084c, original protected visibility.
        public long Value
        {
            get { return m_value; } // 0x060010b8.
            set { m_value = value; } // 0x060010b9.
        }
        // 0x060010ba. Spawn occurs before the source field is read.
        public override IJsonObject Clone()
        {
            JSONLong result = Spawn();
            result.m_value = m_value;
            return result;
        }
        // 0x060010bb.
        public static JSONLong Create(long value)
        {
            JSONLong result = Spawn();
            result.m_value = value;
            return result;
        }
        // 0x060010bc. Original serializer result is discarded.
        public override bool Encode(StringBuilder builder)
        {
            JSONSerializer.SerializeNumber(m_value, builder);
            return true;
        }
        // 0x060010bd, exact original builder/value parameter order and void return.
        public static void Encode(StringBuilder builder, long value)
        {
            JSONSerializer.SerializeNumber(value, builder);
        }
        // 0x060010be, original field reset; no base Reset call.
        protected override void Reset() { m_value = 0; }
        // 0x060010bf, original pooled base constructor only.
        public JSONLong() { }
    }
}
