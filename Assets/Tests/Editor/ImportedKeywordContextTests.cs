using NUnit.Framework;
using UnityEditor;

namespace ProjectLucid.Tests
{
    // Controls for the target-qualified keyword expectation (#524): only the Core RP predicate's targets keep the
    // foveated name, and every other difference from the original tables still fails.
    public sealed class ImportedKeywordContextTests
    {
        private const string Foveated = ImportedKeywordContext.Foveated;

        [Test]
        public void OnlyTheCoreRpPredicateTargetsDeclareFoveated()
        {
            Assert.That(ImportedKeywordContext.DeclaresFoveatedFor(BuildTarget.StandaloneOSX), Is.True);
            Assert.That(ImportedKeywordContext.DeclaresFoveatedFor(BuildTarget.iOS), Is.True);
            Assert.That(ImportedKeywordContext.DeclaresFoveatedFor(BuildTarget.VisionOS), Is.True);
            Assert.That(ImportedKeywordContext.DeclaresFoveatedFor(BuildTarget.PS5), Is.True);
            // The shader compiler names tvOS UNITY_PLATFORM_TVOS, a macro the Core RP predicate does not test.
            Assert.That(ImportedKeywordContext.DeclaresFoveatedFor(BuildTarget.tvOS), Is.False);
            Assert.That(ImportedKeywordContext.DeclaresFoveatedFor(BuildTarget.StandaloneWindows64), Is.False);
            Assert.That(ImportedKeywordContext.DeclaresFoveatedFor(BuildTarget.StandaloneLinux64), Is.False);
            Assert.That(ImportedKeywordContext.DeclaresFoveatedFor(BuildTarget.Android), Is.False);
        }

        [Test]
        public void ImportedMaskDropsOnlyFoveatedWhereItCannotBeDeclared()
        {
            string[] original = { "FOG_LINEAR", Foveated, "INSTANCING_ON" };
            Assert.That(ImportedKeywordContext.Imported(original, true), Is.EqualTo(original));
            Assert.That(ImportedKeywordContext.Imported(original, false), Is.EqualTo(new[] { "FOG_LINEAR", "INSTANCING_ON" }));
            // An unexpected extra name or a missing unrelated name still breaks equivalence.
            Assert.That(new[] { "FOG_LINEAR", "INSTANCING_ON", "FOG_EXP" },
                Is.Not.EquivalentTo(ImportedKeywordContext.Imported(original, false)));
            Assert.That(new[] { "FOG_LINEAR" }, Is.Not.EquivalentTo(ImportedKeywordContext.Imported(original, false)));
        }

        [Test]
        public void KeywordMismatchesAreRejectedInEitherContext()
        {
            // Supported context: foveated must be present and dynamic, as the original flag 0x02 records.
            Assert.That(ImportedKeywordContext.Mismatch(Foveated, true, true, true, 2, true), Is.Null);
            Assert.That(ImportedKeywordContext.Mismatch(Foveated, false, false, false, 2, true), Is.Not.Null);
            Assert.That(ImportedKeywordContext.Mismatch(Foveated, true, false, true, 2, true), Is.Not.Null);
            // Unsupported context: foveated must be absent.
            Assert.That(ImportedKeywordContext.Mismatch(Foveated, false, false, false, 2, false), Is.Null);
            Assert.That(ImportedKeywordContext.Mismatch(Foveated, true, true, false, 2, false), Is.Not.Null);
            // Other names stay exact regardless of context.
            Assert.That(ImportedKeywordContext.Mismatch("FOG_LINEAR", true, false, true, 0, false), Is.Null);
            Assert.That(ImportedKeywordContext.Mismatch("FOG_LINEAR", false, false, true, 0, false), Is.Not.Null);
            Assert.That(ImportedKeywordContext.Mismatch("FOG_LINEAR", true, true, true, 0, false), Is.Not.Null);
            Assert.That(ImportedKeywordContext.Mismatch("FOG_LINEAR", true, false, false, 0, true), Is.Not.Null);
        }
    }
}
