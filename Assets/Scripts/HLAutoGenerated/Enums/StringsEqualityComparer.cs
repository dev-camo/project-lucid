using System.Collections.Generic;

namespace Hardlight.Enums
{
    // Original public, nonsealed, BeforeFieldInit type; zero fields/attributes.
    public class StringsEqualityComparer : IEqualityComparer<Strings>
    {
        // 0x060000d5; original ARM64 0x19eb638 (Equals).
        public bool Equals(Strings a, Strings b) => a == b;

        // 0x060000d6; original ARM64 0x19eb644 (GetHashCode).
        public int GetHashCode(Strings a) => (int)a;

        // 0x060000d7; original ARM64 0x19ea88c (.ctor).
        public StringsEqualityComparer() { }
    }
}
