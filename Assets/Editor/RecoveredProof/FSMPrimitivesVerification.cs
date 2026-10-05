#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;

namespace ProjectLucid
{
    // Source-based checks against constants and control flow traced from native
    // methods. This is not a run of the original game's interpreter or gameplay.
    public static class FSMPrimitivesVerification
    {
        public static void Run()
        {
            Check(HLCRC32.Generate((string)null) == 0u, "null string");
            Check(HLCRC32.Generate(string.Empty) == 0u, "empty string");
            Check(HLCRC32.Generate((byte[])null) == 0u, "null bytes");
            Check(HLCRC32.Generate(new byte[0]) == 0u, "empty bytes");
            Check(HLCRC32.Generate("123456789", HLCRC32.Case.AsIs) == 0x340bc6d9u, "raw CRC without complement");
            Check(HLCRC32.Generate("abc") == 0x5c7cfcb7u, "default uppercase");
            Check(HLCRC32.Generate("ABC", HLCRC32.Case.Lower) == 0xcadbbe3du, "lowercase");
            Check(HLCRC32.Generate("AbC", (HLCRC32.Case)99) == 0xc9f8d815u, "unknown case preserves input");
            Check(HLCRC32.Generate("\u0100", HLCRC32.Case.AsIs) == 0x2dfd1072u, "low UTF-16 byte");
            Check(HLCRC32.Generate(new byte[] { 0, 255, 128, 1 }) == 0x2ca7bcd3u, "binary input");
            Check(HLCRC32.GenerateInt("abc", HLCRC32.Case.AsIs) == unchecked((int)0xcadbbe3du), "signed CRC result");

            // These distinct strings naturally collide because each code unit
            // contributes only eight bits. No test-only injection is required.
            string first = "Lucid-recovery-collision-A";
            string second = "Lucid-recovery-collision-\u0141";
            Check(HLCRC32.Generate(first, HLCRC32.Case.AsIs) == HLCRC32.Generate(second, HLCRC32.Case.AsIs), "natural CRC collision");
            int firstId = GraphNameLookup.ConvertNameToId(first);
            int secondId = GraphNameLookup.ConvertNameToId(second);
            Check(secondId == unchecked(firstId + 1), "collision probes next ID");
            Check(GraphNameLookup.ConvertNameToId(second) == secondId, "collision lookup reuses ID");
            Check(GraphNameLookup.LookupNameUsingId(firstId) == first, "reverse lookup first name");
            Check(GraphNameLookup.LookupNameUsingId(secondId) == second, "reverse lookup colliding name");
            Check(GraphNameLookup.ConvertNameToId(null) == 0 && GraphNameLookup.ConvertNameToId("") == 0, "default IDs");
            Check(GraphNameLookup.LookupNameUsingId(0) == "", "zero lookup");

            FSMIdentifier named = first;
            Check(named.Name == first && named.Id == firstId && (int)named == firstId, "lazy named identifier");
            FSMIdentifier numbered = secondId;
            Check(numbered.Id == secondId && numbered.Name == second && (string)numbered == second, "lazy numbered identifier");
            Check(default(FSMIdentifier).Id == 0 && default(FSMIdentifier).Name == "", "default identifier");

            var key = new GraphStorageKey("Lucid-recovery-value", "Lucid-recovery-node", "Lucid-recovery-graph");
            var equal = new GraphStorageKey(key.NameId, key.NodeId, key.GraphId);
            Check(key.Equals(equal) && key.Equals((object)equal), "key identity");
            Check(!key.Equals(new GraphStorageKey(key.NameId, key.GraphId, key.NodeId)), "key field order");
            Check(!key.Equals(null) && !key.Equals(key.NameId), "boxed key type");
            Check(key.GetHashCode() == HashCode.Combine(key.GraphId, key.NodeId, key.NameId), "cached library hash");
            var map = new Dictionary<GraphStorageKey, int> { [key] = 123 };
            Check(map[equal] == 123, "storage dictionary key");
            Check(key.ToString() == "Lucid-recovery-graph_Lucid-recovery-node_Lucid-recovery-value", "key string field order");
            Check(new GraphStorageKey(key.NameId).ToString() == "null_null_Lucid-recovery-value", "omitted graph and node");
            Check(default(GraphStorageKey).ToString() == "null_null_" && default(GraphStorageKey).GetHashCode() == 0, "default key");

            var context = new FSMUpdateContext(-0.125f, FSMUpdateType.Manual);
            Check(context.DeltaTime == -0.125f && context.UpdateType == FSMUpdateType.Manual, "explicit update context");
            Check(new FSMUpdateContext(2.5f).UpdateType == FSMUpdateType.Update, "default update type");
            Check((int)FSMUpdateType.InitialiseUser == 3 && (int)FSMActionReason.Forced == 4, "enum identities");
            Debug.Log("FSM primitive source-based verification passed. Unknown-name logging and the full original-game interpreter remain unverified.");
        }

        private static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FSM primitive verification failed: " + name);
        }
    }
}
#endif
