using System;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000167; JSON shape of the packaged and
    // downloaded country regulation index. Bodies from the 1.10.1 x86-64 binary.
    [Serializable]
    public sealed class CountryRegulationIndexAPI
    {
        // Original 0x02000168.
        [Serializable]
        public sealed class Country
        {
            public string id = string.Empty;

            public int age;

            public string type = string.Empty;

            public int overAge = -1;

            // 0x060009d1 (RVA 0x1AF80A0): reference-null left side compares the
            // right side by reference, otherwise the virtual Equals decides.
            public static bool operator ==(Country lhs, Country rhs)
            {
                if (lhs is null) return rhs is null;
                return lhs.Equals(rhs);
            }

            // 0x060009d2 (RVA 0x1AF80D0).
            public static bool operator !=(Country lhs, Country rhs) => !(lhs == rhs);

            // 0x060009d3 (RVA 0x1AF8100): id uses string ==, type uses the
            // instance string.Equals, so a null type on this side throws.
            public override bool Equals(object obj)
            {
                if (obj == null) return false;
                if (GetType() != obj.GetType()) return false;
                Country other = (Country)obj;
                return id == other.id && age == other.age && type.Equals(other.type) && overAge == other.overAge;
            }

            // 0x060009d4 (RVA 0x1AF8210).
            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = id != null ? id.GetHashCode() : 0;
                    hashCode = (hashCode * 397) ^ age;
                    hashCode = (hashCode * 397) ^ (type != null ? type.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ overAge;
                    return hashCode;
                }
            }

            // 0x060009d5 (RVA 0x1AF8290): field initialisers only.
            public Country()
            {
            }
        }

        public Country[] countries;

        // 0x060009d0 (RVA 0x1AF8090).
        public CountryRegulationIndexAPI()
        {
        }
    }
}
