using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // Fixtures that build FiniteStateMachine, state, transition and key objects register a fresh GUID-suffixed name in the
    // process-wide GraphNameLookup table for every case. SharedStateSnapshot excludes that table, so nothing else releases the
    // names. This base class opens a SyntheticGraphNameScope with the fixture's name prefixes around every case, and the scope
    // removes only the prefixed names that were absent beforehand, also when the case fails. Names registered earlier by other
    // code and production keys created lazily during the case are left as they are. (A class-level NUnit ITestAction is not
    // used: Test Framework 1.1.33 reads those from the test method only; see SharedStateSnapshot.)
    public abstract class OwnedGraphNamesFixture
    {
        private readonly string[] prefixes;
        private SyntheticGraphNameScope names;

        protected OwnedGraphNamesFixture(params string[] syntheticPrefixes) => prefixes = syntheticPrefixes;

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
