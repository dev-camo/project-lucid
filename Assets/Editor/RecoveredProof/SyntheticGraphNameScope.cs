using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;

namespace ProjectLucid
{
    // Verification helpers register unique synthetic FSM names in the process-wide GraphNameLookup table (field s_nameLookup,
    // Private|Static|InitOnly, .cctor 0x060008af). Dispose removes only the entries that were absent when the scope opened and
    // whose name starts with one of the scope's prefixes. Names other code registered earlier, and legitimate production keys
    // created lazily during the run (for example stateHistory), are left exactly as they are. SharedStateSnapshot excludes the
    // table, so this scope is what keeps a proof from leaking names.
    public sealed class SyntheticGraphNameScope : IDisposable
    {
        private readonly Dictionary<int, string> table = (Dictionary<int, string>)typeof(GraphNameLookup)
            .GetField("s_nameLookup", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        private readonly HashSet<int> prior;
        private readonly string[] prefixes;

        public SyntheticGraphNameScope(params string[] syntheticPrefixes)
        {
            prefixes = syntheticPrefixes;
            prior = new HashSet<int>(table.Keys);
        }

        public void Dispose()
        {
            foreach (KeyValuePair<int, string> entry in table.ToList())
            {
                if (!prior.Contains(entry.Key) && entry.Value != null && prefixes.Any(prefix => entry.Value.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    table.Remove(entry.Key);
                }
            }
        }
    }
}
