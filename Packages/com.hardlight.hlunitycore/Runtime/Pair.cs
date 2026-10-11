// Preserved Sonic Dream Team 1.10.1, HLUnityCore.Runtime.dll.
// Original Pair open MethodDef pointers are zero. Addresses below are
// registered ARM64 specialized/shared, then x86_64 specialized/shared bodies.
// 0x060010ea 0x18cc0f0, 0x18cc4ac, 0x18c7bf0, 0x18c7f70
// 0x060010eb 0x18cc0f8, 0x18cc4b4, 0x18c7c00, 0x18c7f80
// 0x060010ec 0x18cc13c, 0x18cc63c, 0x18c7c40, 0x18c80d0
// 0x060010ed 0x18cc330, 0x18ccaa0, 0x18c7e40, 0x18c84d0
// 0x060010ee 0x18cc338, 0x18ccae4, 0x18c7e50, 0x18c8520
// 0x060010ef 0x18cc340, 0x18ccba8, 0x18c7e60, 0x18c85c0
// 0x060010f0 0x18cc348, 0x18ccbf0, 0x18c7e70, 0x18c8610
// 0x060010f1 0x18cc350, 0x18cccb8, 0x18c7e90, 0x18c86c0
// Original HLUnityCore.Runtime Hardlight.Generics.Pair<T, U>, 0x0200029f.
// Eight original methods 0x060010ea..0x060010f1. Open pointers are zero;
// registered class-instantiated implementations supply both CPU body families.
// Instance mutates one eager, mutable pair per closed type and rereads that
// static field for each assignment and the return. Callers must not treat it
// as a fresh allocation or retain immutable snapshots of its two properties.
// Automatic properties preserve their compiler-generated field identities.
// No constraints, direct interfaces, nested owners or optional defaults exist.
// Source/native preservation; regenerated generic dispatch and metadata remain
// separate from maintained compilation and runtime verification.
namespace Hardlight.Generics
{
    public class Pair<T, U>
    {
        public Pair()
        {
        }

        public Pair(T first, U second)
        {
            First = first;
            Second = second;
        }

        public static Pair<T, U> Instance(T first, U second)
        {
            s_instance.First = first;
            s_instance.Second = second;
            return s_instance;
        }

        public T First { get; set; }
        public U Second { get; set; }

        private static Pair<T, U> s_instance = new Pair<T, U>();
    }
}
