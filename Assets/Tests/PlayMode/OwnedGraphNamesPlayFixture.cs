using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // Play Mode counterpart of the Edit Mode OwnedGraphNamesFixture. The Edit Mode scope (SyntheticGraphNameScope) lives in the
    // editor-only verification assembly, which Play Mode tests cannot reference. Every case that registers a GUID-suffixed
    // FSM name in the process-wide GraphNameLookup table (field s_nameLookup, Private|Static|InitOnly) removes, afterwards, only
    // the entries that were absent beforehand and whose name starts with one of the fixture's prefixes, also when the case
    // fails. Names registered earlier by other code and lazily created production keys are left as they are.
    public abstract class OwnedGraphNamesPlayFixture
    {
        private static readonly Dictionary<int, string> Table = (Dictionary<int, string>)typeof(GraphNameLookup)
            .GetField("s_nameLookup", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        private readonly string[] prefixes;
        private HashSet<int> prior;

        protected OwnedGraphNamesPlayFixture(params string[] syntheticPrefixes) => prefixes = syntheticPrefixes;

        [SetUp]
        public void OpenOwnedGraphNames() => prior = new HashSet<int>(Table.Keys);

        [TearDown]
        public void ReleaseOwnedGraphNames()
        {
            foreach (KeyValuePair<int, string> entry in Table.ToList())
                if (!prior.Contains(entry.Key) && entry.Value != null && prefixes.Any(prefix => entry.Value.StartsWith(prefix, StringComparison.Ordinal)))
                    Table.Remove(entry.Key);
            prior = null;
        }
    }
}
