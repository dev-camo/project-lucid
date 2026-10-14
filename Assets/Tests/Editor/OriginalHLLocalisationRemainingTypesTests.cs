using System.Linq;
using System.Reflection;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Editor
{
    // OptionallyLocalisedString, ReverseOrderOnLanguage and FontAssetLanguageFallbackData from
    // the original 1.10.1 HLLocalisation.Runtime. String-table lookups are not exercised here.
    public sealed class OriginalHLLocalisationRemainingTypesTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void LiteralContentFormatsNestedParametersAndKeepsTextOnFormatErrors()
        {
            var text = new OptionallyLocalisedString("{0} of {1}", false);
            Assert.That(text.HasContent(), Is.True);
            OptionallyLocalisedString first = text.AddParameter("3", false);
            Assert.That(first.ToString(), Is.EqualTo("3"), "AddParameter returns the new parameter");
            var second = new OptionallyLocalisedString("{0}!", false);
            Assert.That(text.AddParameter(second), Is.SameAs(second));
            second.AddParameter("5", false);
            Assert.That(text.ToString(), Is.EqualTo("3 of 5!"));

            var broken = new OptionallyLocalisedString("{0} {1}", false);
            broken.AddParameter("only one", false);
            Assert.That(broken.ToString(), Is.EqualTo("{0} {1}"), "a FormatException leaves the unformatted text");
            Assert.That(new OptionallyLocalisedString(string.Empty, false).HasContent(), Is.False);
        }

        [Test]
        public void TableContentIsStoredAsItsCrcStringId()
        {
            var table = new OptionallyLocalisedString("MENU_TITLE", true);
            Assert.That(typeof(OptionallyLocalisedString).GetField("stringID", Instance).GetValue(table),
                Is.EqualTo((Strings)HLCRC32.GenerateInt("MENU_TITLE")));
            Assert.That(typeof(OptionallyLocalisedString).GetField("content", Instance).GetValue(table), Is.Null);
            Assert.That(new OptionallyLocalisedString(Strings.NONE).HasContent(), Is.False);
            Assert.That(typeof(OptionallyLocalisedString).GetField("stringID", Instance).GetValue(new OptionallyLocalisedString("x", false)),
                Is.EqualTo(Strings.NONE), "the field initializer is Strings.NONE");
        }

        [Test]
        public void ReverseChildrenReversesSiblingOrder()
        {
            var parent = new GameObject("Lucid reverse");
            try
            {
                foreach (string name in new[] { "a", "b", "c", "d" })
                    new GameObject(name).transform.SetParent(parent.transform);
                var reverse = parent.AddComponent<ReverseOrderOnLanguage>();
                typeof(ReverseOrderOnLanguage).GetMethod("ReverseChildren", Instance).Invoke(reverse, null);
                Assert.That(Enumerable.Range(0, 4).Select(i => parent.transform.GetChild(i).name), Is.EqualTo(new[] { "d", "c", "b", "a" }));
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void DeclarationsMatchTheShippedAssembly()
        {
            Assert.That((int)typeof(ReverseOrderOnLanguage).Attributes, Is.EqualTo(0x00100101));
            Assert.That((int)typeof(FontAssetLanguageFallbackData).Attributes, Is.EqualTo(0x00100101));
            Assert.That(typeof(FontAssetLanguageFallbackData).Namespace, Is.Null);
            Assert.That(typeof(FontAssetLanguageFallbackData).GetProperty("AllLangaugeFallBacksCollection"), Is.Not.Null);
            Assert.That(typeof(FontAssetLanguageFallbackData).GetNestedTypes().Select(type => type.Name),
                Is.EquivalentTo(new[] { "FontAssetFallbackPairing", "FontAssetAndFallBacks", "AllLanguageFallBacks" }));
        }
    }
}
