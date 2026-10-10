using System;
namespace ProjectLucid.Verification
{
    public static partial class ExclusionListVerification
    {
        const int CurrentOwnerFlags = 1056769;
        // Literal complete Core243 main tokens remain provenance, never compared to a different compiled module.
        const int RecordedOwnerToken = 33554710;
        const int RecordedPropertyToken = 385876161;
        static FieldFact[] CurrentFields() => new[] {
            new FieldFact("Name", "System.String", 6, 67109489, "The name of the Exclusion List. This can be used to enable/disable the exclusions as required."),
            new FieldFact("Exclusions", "System.String[]", 6, 67109490, "A list of strings (can be partial) to compare each log entry to. If a log entry contains one of these strings, it will be ignored."),
            new FieldFact("m_enabled", "System.Boolean", 1, 67109491, null),
        };
        static MethodFact[] CurrentMethods() => new[] {
            new MethodFact("set_Enabled", "System.Void Hardlight.ExclusionList::set_Enabled(System.Boolean)", 2182, 100665475, "System.Void", new string[] { "value" }, new string[] { "System.Boolean" }, "02037D730200042A", "22", 8, false, 0, new string[] {  }, new TokenFact[] { new TokenFact(3, 67109491, "System.Boolean Hardlight.ExclusionList::m_enabled", "HLUnityCore.Runtime.dll") }),
            new MethodFact("IsExcluded", "System.Boolean Hardlight.ExclusionList::IsExcluded(System.String)", 134, 100665476, "System.Boolean", new string[] { "message" }, new string[] { "System.String" }, "027B730200042D02162A160A2B1603027B72020004069A6FBF00000A2C02172A0617580A06027B720200048E6932DF162A", "133003003100000037000011", 3, true, 285212727, new string[] { "System.Int32" }, new TokenFact[] { new TokenFact(2, 67109491, "System.Boolean Hardlight.ExclusionList::m_enabled", "HLUnityCore.Runtime.dll"), new TokenFact(17, 67109490, "System.String[] Hardlight.ExclusionList::Exclusions", "HLUnityCore.Runtime.dll"), new TokenFact(24, 167772351, "System.Boolean System.String::Contains(System.String)", "netstandard"), new TokenFact(39, 67109490, "System.String[] Hardlight.ExclusionList::Exclusions", "HLUnityCore.Runtime.dll") }),
            new MethodFact(".ctor", "System.Void Hardlight.ExclusionList::.ctor()", 6278, 100665477, "System.Void", new string[] {  }, new string[] {  }, "02177D7302000402283300000A2A", "3a", 8, false, 0, new string[] {  }, new TokenFact[] { new TokenFact(3, 67109491, "System.Boolean Hardlight.ExclusionList::m_enabled", "HLUnityCore.Runtime.dll"), new TokenFact(9, 167772211, "System.Void System.Object::.ctor()", "netstandard") }),
        };
    }
}
