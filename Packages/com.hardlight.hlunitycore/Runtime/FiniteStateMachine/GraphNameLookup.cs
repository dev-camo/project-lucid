using System;
using System.Collections.Generic;

namespace Hardlight
{
    public static class GraphNameLookup
    {
        // HLUnityCore.Runtime.dll:Hardlight.GraphNameLookup:0x060008af;
        // arm64 0x1af1f44: default Dictionary<int,string> constructor.
        private static readonly Dictionary<int, string> s_nameLookup = new Dictionary<int, string>();
        private const int DefaultId = 0;

        // 0x060008ad; arm64 0x1af1c2c. Names are case sensitive; collisions
        // probe successive signed IDs with unchecked wraparound.
        public static int ConvertNameToId(string name)
        {
            if (string.IsNullOrEmpty(name)) return DefaultId;
            int id = HLCRC32.GenerateInt(name, HLCRC32.Case.AsIs);
            while (s_nameLookup.TryGetValue(id, out string existing))
            {
                if (existing.Equals(name)) return id;
                id = unchecked(id + 1);
            }
            s_nameLookup[id] = name;
            return id;
        }

        // 0x060008ae; arm64 0x1af1dd0. Unknown IDs log through the original
        // process boundary before returning the original fallback.
        public static string LookupNameUsingId(int id)
        {
            if (id == DefaultId) return string.Empty;
            if (s_nameLookup.TryGetValue(id, out string name)) return name;
            HLOutput.LogError(string.Format("Failed to find name for id {0}. Returning 'unknown'.", id));
            return "unknown";
        }
    }
}
