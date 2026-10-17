using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Original HLUnityCore.Runtime KeyValueParser 0x0200027c and BuildMetaData 0x02000021,
    // checked against the 1.10.1 x86-64 bodies. Every parser and BuildMetaData is owned by
    // the test that builds it; neither type has static state.
    public sealed class OriginalKeyValueParserTests
    {
        // 0x1b1f7c0 stores m_trimWhiteSpace = 1 and the packed separators 0x3d000a
        // ('\n', '='); m_trimNullValues is left false.
        [Test]
        public void DefaultParserSplitsLinesAtTheFirstEqualsAndTrimsKeysAndValues()
        {
            Dictionary<string, string> result = new KeyValueParser().Parse(" version = 1.10.1 \nurl=a=b\n\n   \nflag");

            Assert.That(result.Count, Is.EqualTo(3));
            Assert.That(result["version"], Is.EqualTo("1.10.1"));
            // String.Split(char[], 2) at 0x1b1f752: only the first separator splits.
            Assert.That(result["url"], Is.EqualTo("a=b"));
            // A line without a separator keeps its key with a null value by default.
            Assert.That(result.ContainsKey("flag"), Is.True);
            Assert.That(result["flag"], Is.Null);
        }

        // 0x1b1f75a and 0x1b1f77d test m_trimWhiteSpace before each Trim call.
        [Test]
        public void DisabledTrimKeepsSurroundingWhitespaceOnKeyAndValue()
        {
            Dictionary<string, string> result = new KeyValueParser().TrimWhiteSpace(false).Parse(" key = value ");

            Assert.That(result.ContainsKey(" key "), Is.True);
            Assert.That(result.ContainsKey("key"), Is.False);
            Assert.That(result[" key "], Is.EqualTo(" value "));
        }

        // 0x1b1f79f: only a missing value is dropped; an empty value is kept.
        [Test]
        public void TrimNullValuesDropsOnlyLinesWithoutASeparator()
        {
            Dictionary<string, string> result = new KeyValueParser().TrimNullValues(true).Parse("missing\nempty=\nset=1");

            Assert.That(result.ContainsKey("missing"), Is.False);
            Assert.That(result["empty"], Is.EqualTo(string.Empty));
            Assert.That(result["set"], Is.EqualTo("1"));
        }

        // Dictionary set_Item at 0x1b1f721: a repeated key overwrites, it does not throw.
        [Test]
        public void RepeatedKeyKeepsTheLastValue()
        {
            Dictionary<string, string> result = new KeyValueParser().Parse("key=first\nkey=second");

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result["key"], Is.EqualTo("second"));
        }

        // Parse(string) splits on m_entrySeparator (+0x12), Parse(string[]) on
        // m_keyValueSeparator (+0x14). Using the default separators as data shows each
        // setter reaches only its own split.
        [Test]
        public void CustomSeparatorsReplaceTheDefaultsForTheirOwnSplitOnly()
        {
            Dictionary<string, string> result = new KeyValueParser()
                .SetEntrySeparator(';')
                .SetKeyValueSeparator(':')
                .Parse("a:1=x;b:2\nc");

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result["a"], Is.EqualTo("1=x"));
            Assert.That(result["b"], Is.EqualTo("2\nc"));
        }

        // Fluent setters return the receiver itself (mov rax, rdi at 0x1b1f554..0x1b1f584).
        [Test]
        public void FluentSettersReturnTheSameParser()
        {
            var parser = new KeyValueParser();

            Assert.That(parser.TrimWhiteSpace(true), Is.SameAs(parser));
            Assert.That(parser.TrimNullValues(true), Is.SameAs(parser));
            Assert.That(parser.SetEntrySeparator('\n'), Is.SameAs(parser));
            Assert.That(parser.SetKeyValueSeparator('='), Is.SameAs(parser));
        }

        // 0x1b1f5d6 returns a new empty dictionary for blank text; 0x1b1f6cc returns the
        // empty dictionary for a null array instead of throwing.
        [Test]
        public void BlankTextAndNullLinesGiveEmptyDictionaries()
        {
            var parser = new KeyValueParser();

            Assert.That(parser.Parse((string)null), Is.Empty);
            Assert.That(parser.Parse(" \n\t"), Is.Empty);
            Assert.That(parser.Parse((string[])null), Is.Empty);
            Assert.That(parser.Parse(new[] { "  ", null, "k=v" }).Count, Is.EqualTo(1));
        }

        // BuildMetaData 0x1a9ea80 returns before parsing when Resources.Load gives null, so
        // GetValue (0x1a9eb80) takes its null m_data branch. The 1.10.1 data ships no
        // Resources/BuildMetaData/BuildMetaData asset, which the precondition confirms.
        [Test]
        public void BuildMetaDataWithoutItsResourceReturnsNullForEveryKey()
        {
            Assert.That(Resources.Load<TextAsset>("BuildMetaData/BuildMetaData"), Is.Null);

            BuildMetaData buildMetaData = BuildMetaData.Create();
            FieldInfo data = typeof(BuildMetaData).GetField("m_data", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(data.GetValue(buildMetaData), Is.Null);
            Assert.That(buildMetaData.GetValue("version"), Is.Null);
        }

        // With data present GetValue is TryGetWithDefault<string, string>(key, null): a
        // stored value is returned and a missing key gives null rather than throwing.
        [Test]
        public void BuildMetaDataLookupReturnsStoredValuesAndNullForMissingKeys()
        {
            var buildMetaData = new BuildMetaData();
            FieldInfo data = typeof(BuildMetaData).GetField("m_data", BindingFlags.Instance | BindingFlags.NonPublic);
            data.SetValue(buildMetaData, new KeyValueParser().Parse("branch=main\nempty="));

            Assert.That(buildMetaData.GetValue("branch"), Is.EqualTo("main"));
            Assert.That(buildMetaData.GetValue("empty"), Is.EqualTo(string.Empty));
            Assert.That(buildMetaData.GetValue("missing"), Is.Null);
        }
    }
}
