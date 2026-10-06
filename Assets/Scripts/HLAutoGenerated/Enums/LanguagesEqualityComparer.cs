using System.Collections.Generic;

namespace Hardlight.Enums
{
    // Original public, nonsealed, BeforeFieldInit type; zero fields/attributes.
    public class LanguagesEqualityComparer : IEqualityComparer<Languages>
    {
        // 0x060000cb; original ARM64 0x19ea7a4 (Equals).
        public bool Equals(Languages a, Languages b) => a == b;

        // 0x060000cc; original ARM64 0x19ea7b0 (GetHashCode).
        public int GetHashCode(Languages a) => (int)a;

        // 0x060000cd; original ARM64 0x19ea7b8 (.ctor).
        public LanguagesEqualityComparer() { }
    }
}
