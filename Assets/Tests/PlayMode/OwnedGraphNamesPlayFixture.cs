using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // Play Mode counterpart of the Edit Mode OwnedGraphNamesFixture. Every case that registers a GUID-suffixed FSM name in the
    // process-wide GraphNameLookup table runs inside the shared SyntheticGraphNameScope, which removes afterwards only the
    // entries that were absent beforehand and whose name starts with one of the fixture's prefixes, also when the case fails.
    public abstract class OwnedGraphNamesPlayFixture
    {
        private readonly string[] prefixes;
        private SyntheticGraphNameScope names;

        protected OwnedGraphNamesPlayFixture(params string[] syntheticPrefixes) => prefixes = syntheticPrefixes;

        [SetUp]
        public void OpenOwnedGraphNames() => names = new SyntheticGraphNameScope(prefixes);

        [TearDown]
        public void ReleaseOwnedGraphNames()
        {
            names?.Dispose();
            names = null;
        }
    }
}
