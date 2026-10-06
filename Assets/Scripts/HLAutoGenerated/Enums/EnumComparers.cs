namespace Hardlight.Enums
{
    // Original 0x02000030 is a normal public BeforeFieldInit class, not static.
    public class EnumComparers
    {
        // 0x040003ac, native static0; original public static readonly concrete type.
        public static readonly LanguagesEqualityComparer LanguagesComparer = new LanguagesEqualityComparer();
        // 0x040003ad, native static8; preserve Language-before-Strings allocation/store.
        public static readonly StringsEqualityComparer StringsComparer = new StringsEqualityComparer();

        // 0x060000ce; original ARM64 0x19ea7c0 (.ctor).
        public EnumComparers() { }

        // 0x060000cf; original ARM64 0x19ea7c8 (.cctor).
        // BeforeFieldInit cctor is emitted from the ordered field initializers above.
    }
}
