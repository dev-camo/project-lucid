using System.Collections;
using System.Reflection;
using Hardlight;
using Hardlight.Localisation;
using NUnit.Framework;

namespace ProjectLucid.Editor
{
    // The language proof must not leave its default registry row behind; a retained empty
    // row makes later cold-state fixtures, such as the string-table proof, order dependent.
    public sealed class LanguageVerificationRegistryOwnershipTests
    {
        [Test]
        public void LanguageProofLeavesNoLanguageRegistryRow()
        {
            var registry = (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            string name = ProcessManager.GetDefaultName<Language>();
            VerificationFixtureOwnership.RequireAbsent(registry, name);
            LanguageVerification.Run();
            Assert.That(registry.Contains(name), Is.False);
            Assert.DoesNotThrow(() => StringTableVerification.RequireColdState(registry, null, null, null, null));
        }
    }
}
