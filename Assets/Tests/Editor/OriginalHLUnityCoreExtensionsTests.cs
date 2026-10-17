using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using Hardlight;
using Hardlight.Utils;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Behaviour of the original HLUnityCore.Runtime string, type, enum, nullable
    // and array helpers against their 1.10.1 x86-64 bodies. Shared static state
    // touched here (the StringExtensions builder, OpString's cached instance and
    // owner thread, and the EnumExtensions cache) is captured in SetUp and restored
    // in TearDown.
    [TestFixture]
    public sealed class OriginalHLUnityCoreExtensionsTests
    {
        // Owned nested generic for ConvertToCode's per-level arity split.
        public sealed class GenericOuter<T>
        {
            public sealed class Inner<U>
            {
            }
        }

        // Owned constrained definition for ConstructWithBaseTypes.
        public sealed class ComponentHolder<T> where T : Component
        {
        }

        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

        private string m_stringBuilderSnapshot;
        private OpStringCacheScope m_opStringCache;
        private Dictionary<UpdateOn, string> m_updateOnCacheSnapshot;

        private static StringBuilder SharedStringBuilder =>
            (StringBuilder)typeof(StringExtensions).GetField("s_stringBuilder", PrivateStatic).GetValue(null);

        private static Dictionary<UpdateOn, string> UpdateOnCache =>
            (Dictionary<UpdateOn, string>)typeof(EnumExtensions).GetField("UpdateOnEnumToString", PrivateStatic).GetValue(null);

        [SetUp]
        public void SetUp()
        {
            m_stringBuilderSnapshot = SharedStringBuilder.ToString();
            // Format0PlaceholderTime reads OpString.i, which clears the cached builder;
            // it runs on an owned cache so the shared builder's content is never touched.
            m_opStringCache = new OpStringCacheScope();
            m_updateOnCacheSnapshot = new Dictionary<UpdateOn, string>(UpdateOnCache);
        }

        [TearDown]
        public void TearDown()
        {
            // Each restore runs even when an earlier one throws.
            try { SharedStringBuilder.Clear().Append(m_stringBuilderSnapshot); }
            finally
            {
                try
                {
                    Dictionary<UpdateOn, string> cache = UpdateOnCache;
                    cache.Clear();
                    foreach (KeyValuePair<UpdateOn, string> entry in m_updateOnCacheSnapshot)
                    {
                        cache.Add(entry.Key, entry.Value);
                    }
                }
                finally
                {
                    OpStringCacheScope scope = m_opStringCache;
                    m_opStringCache = null;
                    scope?.Dispose();
                }
            }
        }

        // ToSnake 0x0600030d (0x1ab3f90) with AppendCharacter 0x06000325 (0x1ab4650)
        // and AppendUnderscore 0x06000326 (0x1ab45e0): the remembered characters keep
        // their original case, the upper-upper-lower rule shifts the last char right,
        // and inner separators collapse while outer ones are trimmed.
        [Test]
        public void ToSnakeSplitsCaseRunsDigitGroupsAndSeparatorsAsShipped()
        {
            Assert.That("HelloWorld".ToSnake(), Is.EqualTo("hello_world"));
            Assert.That("XMLParser".ToSnake(), Is.EqualTo("xml_parser"));
            Assert.That("Level10Boss".ToSnake(), Is.EqualTo("level_10_boss"));
            Assert.That("Level10Boss".ToSnake(null, false, false), Is.EqualTo("level10boss"), "no case rule fires after a digit");
            Assert.That("Level10Boss".ToSnake(null, true, false), Is.EqualTo("level_10boss"));
            Assert.That("Level10Boss".ToSnake(null, false, true), Is.EqualTo("level10_boss"));
            Assert.That("  __Foo  Bar!!  ".ToSnake(), Is.EqualTo("foo_bar"));
            Assert.That("!!".ToSnake(), Is.EqualTo(string.Empty));
            Assert.That(string.Empty.ToSnake(), Is.SameAs(string.Empty));
            Assert.That(((string)null).ToSnake(), Is.Null);
            Assert.That("I".ToSnake(new CultureInfo("tr-TR")), Is.EqualTo("ı"), "the culture reaches Char.ToLower");
            Assert.That("I".ToSnake(), Is.EqualTo("i"), "a null culture becomes the invariant culture");
        }

        // AddSpaces 0x06000311 (0x1ab4880): a space after a non-upper non-space
        // before upper case, or before the last capital of a run followed by lower.
        [Test]
        public void AddSpacesSeparatesWordsAndCapitalRunsButNotAfterSpaces()
        {
            Assert.That("HelloWorld".AddSpaces(), Is.EqualTo("Hello World"));
            Assert.That("XMLParser".AddSpaces(), Is.EqualTo("XML Parser"));
            Assert.That("a B".AddSpaces(), Is.EqualTo("a B"));
            Assert.That("ABC".AddSpaces(), Is.EqualTo("ABC"));
            Assert.That(" ".AddSpaces(), Is.EqualTo(" "));
        }

        // Format0PlaceholderTime 0x06000318 (0x1ab5220) and Get0PlaceholderFormat
        // 0x06000319 (0x1ab53a0): absolute values padded to DigitCount 2, longer
        // numbers are not cut, one '-' when either part is negative and allowed.
        [Test]
        public void Format0PlaceholderTimePadsAbsolutePartsWithOneLeadingSign()
        {
            Assert.That(string.Empty.Format0PlaceholderTime(5, 7), Is.EqualTo("05:07"));
            Assert.That("T".Format0PlaceholderTime(-1, 5), Is.EqualTo("T-01:05"));
            Assert.That("T".Format0PlaceholderTime(1, -5), Is.EqualTo("T-01:05"));
            Assert.That("T".Format0PlaceholderTime(-1, -5, false), Is.EqualTo("T01:05"));
            Assert.That(string.Empty.Format0PlaceholderTime(123, 0), Is.EqualTo("123:00"));
            Assert.That(string.Empty.Format0PlaceholderTime(10, 59), Is.EqualTo("10:59"));
        }

        // Truncate 0x06000312, TrimEndString 0x06000321, TryGetCharacter 0x06000317,
        // IsIndexValid 0x06000322: TryGetCharacter tests a negative index before
        // touching the text, so only a non-negative index throws on null.
        [Test]
        public void SmallStringHelpersKeepTheirNullAndBoundaryOrder()
        {
            Assert.That("abcdef".Truncate(3, "..."), Is.EqualTo("abc..."));
            Assert.That("abc".Truncate(3, "..."), Is.EqualTo("abc"));

            Assert.That("Zone_NAME".TrimEndString("_NAME"), Is.EqualTo("Zone"));
            Assert.That("Zone_NAME_NAME".TrimEndString("_NAME"), Is.EqualTo("Zone_NAME"));
            Assert.That("Zone".TrimEndString(null), Is.EqualTo("Zone"));
            Assert.That(((string)null).TrimEndString(null), Is.Null);

            Assert.That(((string)null).TryGetCharacter(-1, out char character), Is.False);
            Assert.That(character, Is.EqualTo('\0'));
            Assert.Throws<NullReferenceException>(() => ((string)null).TryGetCharacter(0, out _));
            Assert.That("ab".TryGetCharacter(1, out character), Is.True);
            Assert.That(character, Is.EqualTo('b'));
            Assert.That("ab".TryGetCharacter(2, out character), Is.False);

            Assert.That(((string)null).IsIndexValid(0), Is.False);
            Assert.That("ab".IsIndexValid(1), Is.True);
            Assert.That("ab".IsIndexValid(2), Is.False);
        }

        // IsUpper/IsLower 0x0600030b/0c, IsBlank 0x0600030f, RemoveWhitespace
        // 0x06000310, ToFloatInvariant 0x0600030e, GetIndentSize 0x0600031b,
        // GetLastNonWhiteSpaceIndex 0x0600031c (index 0 is never inspected).
        [Test]
        public void ClassificationAndWhitespaceHelpersMatchTheNativeScans()
        {
            Assert.That("ABC1".IsUpper(), Is.True);
            Assert.That("aB".IsUpper(), Is.False);
            Assert.That(string.Empty.IsLower(), Is.True);
            Assert.That("ab1".IsLower(), Is.True);
            Assert.That(string.Empty.IsBlank(), Is.True);
            Assert.That(" \t".IsBlank(), Is.True);
            Assert.That(" x ".IsBlank(), Is.False);
            Assert.Throws<NullReferenceException>(() => ((string)null).IsBlank());

            Assert.That(" a b\tc \n".RemoveWhitespace(), Is.EqualTo("abc"));
            Assert.That("  ".ToFloatInvariant(), Is.EqualTo(0f));
            Assert.That("1.5".ToFloatInvariant(), Is.EqualTo(1.5f));

            Assert.That("  a".GetIndentSize(), Is.EqualTo(2));
            Assert.That("   ".GetIndentSize(), Is.EqualTo(3));
            Assert.That(((string)null).GetIndentSize(), Is.EqualTo(0));
            Assert.That("ab  ".GetLastNonWhiteSpaceIndex(), Is.EqualTo(1));
            Assert.That(" ".GetLastNonWhiteSpaceIndex(), Is.EqualTo(0));
            Assert.That("   ".GetLastNonWhiteSpaceIndex(), Is.EqualTo(0));
        }

        // MakeNumeric 0x06000313/14, MakeAlphanumeric 0x06000315/16 and
        // HasSpecialChars 0x0600031a, all via ArrayExtensions.Contains<char>.
        [Test]
        public void CharacterFiltersHonourTheirExceptionLists()
        {
            Assert.That("a1-2b".MakeNumeric('-'), Is.EqualTo("1-2"));
            Assert.That("a1-2b".MakeNumeric(), Is.EqualTo("12"));
            Assert.That(" ".MakeNumeric(), Is.EqualTo(" "));
            Assert.That("a_b-c".MakeAlphanumeric('_'), Is.EqualTo("a_bc"));
            Assert.That("ab_c".HasSpecialChars(new[] { '_' }), Is.False);
            Assert.That("ab_c".HasSpecialChars(), Is.True);
            Assert.That(string.Empty.HasSpecialChars(), Is.False);
        }

        // TryRemoveFirst/LastSubstring 0x0600031d/1e and TryInsertFirst/LastText
        // 0x0600031f/20: newValue is the input on every failed path.
        [Test]
        public void SubstringEditsReportFailureWithTheOriginalValue()
        {
            Assert.That("a-b-c".TryRemoveFirstSubstring("-", out string newValue), Is.True);
            Assert.That(newValue, Is.EqualTo("ab-c"));
            Assert.That("a-b-c".TryRemoveLastSubstring("-", out newValue), Is.True);
            Assert.That(newValue, Is.EqualTo("a-bc"));
            Assert.That("-".TryRemoveFirstSubstring("-", out newValue), Is.True);
            Assert.That(newValue, Is.EqualTo(string.Empty));
            Assert.That("abc".TryRemoveFirstSubstring("x", out newValue), Is.False);
            Assert.That(newValue, Is.EqualTo("abc"));
            Assert.That("ab".TryRemoveLastSubstring("abc", out newValue), Is.False);
            Assert.That(newValue, Is.EqualTo("ab"));

            Assert.That("  x  ".TryInsertFirstText("#", out newValue), Is.True);
            Assert.That(newValue, Is.EqualTo("  #x  "));
            Assert.That("  x  ".TryInsertLastText("#", out newValue), Is.True);
            Assert.That(newValue, Is.EqualTo("  x#  "));
            Assert.That("   ".TryInsertLastText("#", out newValue), Is.True);
            Assert.That(newValue, Is.EqualTo("#   "));
            Assert.That("x".TryInsertFirstText(string.Empty, out newValue), Is.False);
            Assert.That(newValue, Is.EqualTo("x"));
        }

        // String ContainsAt 0x06000323 (0x1ab5db0) and StringBuilder ContainsAt
        // 0x0600030a (0x1ab3ad0): index may equal Length, an empty sequence only
        // matches an empty target, and running past the end is a mismatch.
        [Test]
        public void ContainsAtMatchesSequencesFromAnIndexOnStringsAndBuilders()
        {
            Assert.That("abc".ContainsAt(1, "bc"), Is.True);
            Assert.That("abc".ContainsAt(2, "cd"), Is.False);
            Assert.That("abc".ContainsAt(4, "c"), Is.False);
            Assert.That("abc".ContainsAt(-1, "a"), Is.False);
            Assert.That("abc".ContainsAt(3, string.Empty), Is.False);
            Assert.That(string.Empty.ContainsAt(0, string.Empty), Is.True);

            StringBuilder builder = new StringBuilder("abc");
            Assert.That(builder.ContainsAt(1, "bc"), Is.True);
            Assert.That(builder.ContainsAt(2, "cd"), Is.False);
            Assert.That(builder.ContainsAt(3, string.Empty), Is.False);
            Assert.That(new StringBuilder().ContainsAt(0, string.Empty), Is.True);
        }

        // StringBuilderExtensions 0x06000306..09 test the condition before the
        // builder, so a false condition never dereferences a null builder.
        [Test]
        public void ConditionalAppendsCheckTheConditionBeforeTheBuilder()
        {
            StringBuilder nullBuilder = null;
            Assert.DoesNotThrow(() => nullBuilder.Append(false, 'x'));
            Assert.DoesNotThrow(() => nullBuilder.AppendLine(false));
            Assert.Throws<NullReferenceException>(() => nullBuilder.Append(true, "x"));

            StringBuilder builder = new StringBuilder();
            builder.Append(true, 'a');
            builder.Append(false, 'b');
            builder.Append(true, "c");
            builder.AppendLine(false, "d");
            builder.AppendLine(true, "e");
            Assert.That(builder.ToString(), Is.EqualTo("ace" + Environment.NewLine));
        }

        // TypeExtensions 0x06000327..2d with lambdas <>c 0x06000330/31.
        [Test]
        public void TypeHelpersFormatAndConstructTypesAsShipped()
        {
            Assert.That(typeof(Action).IsDelegate(), Is.True);
            Assert.That(typeof(MulticastDelegate).IsDelegate(), Is.True);
            Assert.That(typeof(Delegate).IsDelegate(), Is.False, "IsSubclassOf excludes Delegate itself");
            Assert.That(((Type)null).IsDelegate(), Is.False);

            Assert.That(typeof(List<int>).GetNameWithoutGenericArity(), Is.EqualTo("List"));
            Assert.That(typeof(string).GetNameWithoutGenericArity(), Is.EqualTo("String"));
            Assert.That(typeof(Dictionary<,>).GetGenericArgumentCount(), Is.EqualTo(2));
            Assert.That(typeof(string).GetGenericArgumentCount(), Is.EqualTo(0));
            Assert.That(typeof(int?).IsNullable(), Is.True);
            Assert.That(typeof(int).IsNullable(), Is.False);
            Assert.That(typeof(int).GetTypeNamespacePrefix(), Is.EqualTo("System."));

            Assert.That(typeof(Dictionary<string, List<int>>).ConvertToCode(),
                Is.EqualTo("System.Collections.Generic.Dictionary<System.String, System.Collections.Generic.List<System.Int32>>"));
            Assert.That(typeof(GenericOuter<int>.Inner<string>).ConvertToCode(),
                Is.EqualTo("ProjectLucid.Tests.OriginalHLUnityCoreExtensionsTests.GenericOuter<System.Int32>.Inner<System.String>"));
            Assert.That(typeof(Environment.SpecialFolder).ConvertToCode(), Is.EqualTo("System.Environment.SpecialFolder"));
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => typeof(List<>).ConvertToCode());
            Assert.That(exception.Message, Is.EqualTo("Non-constructed generic types are not supported, please construct the Type: " + typeof(List<>)));

            Assert.That(typeof(List<>).ConstructWithBaseTypes(), Is.EqualTo(typeof(List<object>)));
            Assert.That(typeof(ComponentHolder<>).ConstructWithBaseTypes(), Is.EqualTo(typeof(ComponentHolder<Component>)), "a parameter's base type is its class constraint");
            Assert.Throws<InvalidOperationException>(() => typeof(List<int>).ConstructWithBaseTypes(), "MakeGenericType on a constructed type");
        }

        // EnumExtensions.GetString 0x06000eea (0x1b1b090): named values come from
        // the .cctor table; an unnamed value is formatted "0x{0:X}" and cached.
        [Test]
        public void UpdateOnGetStringUsesTheTableAndCachesUnnamedValues()
        {
            Assert.That(UpdateOn.FixedUpdate.GetString(), Is.EqualTo("FixedUpdate"));
            Assert.That(UpdateOn.LateUpdate.GetString(), Is.EqualTo("LateUpdate"));
            Assert.That(UpdateOn.None.GetString(), Is.EqualTo("None"));
            Assert.That(UpdateOn.Update.GetString(), Is.EqualTo("Update"));

            UpdateOn unnamed = (UpdateOn)0x1234;
            Assert.That(UpdateOnCache.ContainsKey(unnamed), Is.False);
            string text = unnamed.GetString();
            Assert.That(text, Is.EqualTo("0x00001234"));
            Assert.That(UpdateOnCache[unnamed], Is.SameAs(text));
            Assert.That(unnamed.GetString(), Is.SameAs(text));
        }

        // NullableExtensions 0x060002d0/d1 (shared 0x982380/0x982460).
        [Test]
        public void NullableHelpersReturnDefaultsForEmptyValues()
        {
            int? none = null;
            Assert.That(none.TryGetValue(out int value), Is.False);
            Assert.That(value, Is.EqualTo(0));
            int? three = 3;
            Assert.That(three.TryGetValue(out value), Is.True);
            Assert.That(value, Is.EqualTo(3));
            Assert.That(none.TryGetWithDefault(7), Is.EqualTo(7));
            Assert.That(none.TryGetWithDefault(), Is.EqualTo(0));
            Assert.That(three.TryGetWithDefault(7), Is.EqualTo(3));
        }

        // ArrayUtils 0x06001154..56 (shared 0x8d8a70, 0x8d8920, 0x8d8d80): an
        // out-of-range RemoveAt fails in Array.Copy before the resize, so the
        // caller's array is left untouched.
        [Test]
        public void ArrayUtilsResizeInPlaceAndRejectOutOfRangeRemovals()
        {
            int[] array = { 1, 2 };
            ArrayUtils.Add(ref array, 3);
            Assert.That(array, Is.EqualTo(new[] { 1, 2, 3 }));
            ArrayUtils.AddRange(ref array, new List<int> { 4, 5 });
            Assert.That(array, Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));

            ArrayUtils.RemoveAt(ref array, 0);
            Assert.That(array, Is.EqualTo(new[] { 2, 3, 4, 5 }));
            ArrayUtils.RemoveAt(ref array, 3);
            Assert.That(array, Is.EqualTo(new[] { 2, 3, 4 }));
            ArrayUtils.RemoveAt(ref array, 1);
            Assert.That(array, Is.EqualTo(new[] { 2, 4 }));

            int[] before = array;
            Assert.Throws<ArgumentOutOfRangeException>(() => ArrayUtils.RemoveAt(ref array, 5));
            Assert.That(array, Is.SameAs(before));
            Assert.That(array, Is.EqualTo(new[] { 2, 4 }));

            int[] single = { 9 };
            ArrayUtils.RemoveAt(ref single, 0);
            Assert.That(single, Is.Empty);
        }

        // UrlUtils.GetEscaped 0x06001017 (0x1b21650): only the Uri parse is guarded.
        [Test]
        public void GetEscapedEscapesPlusOnlyBeforeTheQueryWhenTheUrlHasSpaces()
        {
            Assert.That(UrlUtils.GetEscaped("not a url"), Is.EqualTo("not a url"));
            Assert.That(UrlUtils.GetEscaped("http://a.com/x+y"), Is.EqualTo("http://a.com/x+y"));
            Assert.That(UrlUtils.GetEscaped("http://a.com/x y+z"), Is.EqualTo("http://a.com/x%20y%2Bz"));
            Assert.That(UrlUtils.GetEscaped("http://a.com/x+y z?q=1+2"), Is.EqualTo("http://a.com/x%2By%20z?q=1+2"));
        }
    }
}
