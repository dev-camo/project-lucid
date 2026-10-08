using System;
using System.Text;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.JSON
{
    // Original HLUnityCore.Runtime 0x02000297, whole seven-method owner.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class JSONDouble : JSONObjectPooled<JSONDouble>
    {
        private double m_value; // 0x04000849.
        public double Value
        {
            get { return m_value; } // 0x06001075.
            set { m_value = value; } // 0x06001076.
        }
        // 0x06001077. Spawn occurs before the source field is read.
        public override IJsonObject Clone()
        {
            JSONDouble result = Spawn();
            result.m_value = m_value;
            return result;
        }
        // 0x06001078.
        public static JSONDouble Create(double value)
        {
            JSONDouble result = Spawn();
            result.m_value = value;
            return result;
        }
        // 0x06001079. Original Encode mutates its value and ignores SerializeNumber's result.
        public override bool Encode(StringBuilder builder)
        {
            m_value = Math.Round(m_value, 4, MidpointRounding.ToEven);
            JSONSerializer.SerializeNumber(m_value, builder);
            return true;
        }
        // 0x0600107a, original field reset; no base Reset call.
        protected override void Reset() { m_value = 0.0; }
        // 0x0600107b, original pooled base constructor only.
        public JSONDouble() { }
    }
}
