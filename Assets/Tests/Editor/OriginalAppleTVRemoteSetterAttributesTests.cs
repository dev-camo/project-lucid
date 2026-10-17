using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Editor
{
    // Compiled field attributes must match the original 1.10.1 Game.Runtime attribute span.
    // This covers declaration identity only, not inspector or tvOS remote behavior.
    public sealed class OriginalAppleTVRemoteSetterAttributesTests
    {
        // DummyDll metadata, Game.Runtime field 0x0400000f m_allowExitToHome:
        // [ShowIfAttribute("m_setOnAwake", (object)(string)null)] then [SerializeField].
        private const string OriginalShowIfBlob = "01000c6d5f7365744f6e4177616b650eff0000";

        // Correctly encoded ShowIf rows on other members. Roslyn emits every null
        // reaching ShowIf's object parameter as 0x0e 0xff (a (Type)null fixture
        // compiles byte-identical to the target), so wrong encodings are tested on
        // mutated copies of the original blob instead of compiled fixtures.
        private sealed class OtherShowIfRowsFixture
        {
#pragma warning disable 0169
            [ShowIf("m_setOnAwake", null)]
            [SerializeField]
            private bool m_first;
            [ShowIf("m_setOnAwake", null)]
            [SerializeField]
            private bool m_second;
            [ShowIf("m_other", null)]
            [SerializeField]
            private bool m_third;
#pragma warning restore 0169
        }

        private static FieldInfo Field(Type owner, string name) =>
            owner.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

        private static CustomAttributeData ShowIf(FieldInfo field) =>
            field.GetCustomAttributesData().Single(attribute => attribute.AttributeType == typeof(ShowIfAttribute));

        private static byte[] Bytes(string hex) =>
            Enumerable.Range(0, hex.Length / 2).Select(index => Convert.ToByte(hex.Substring(index * 2, 2), 16)).ToArray();

        [Test]
        public void AllowExitToHomeRetainsOriginalOrderedAttributes() // field token 0x0400000f
        {
            FieldInfo field = Field(typeof(AppleTVRemoteSetter), "m_allowExitToHome");
            Assert.That(field, Is.Not.Null);
            CustomAttributeData[] attributes = field.GetCustomAttributesData().ToArray();
            Assert.That(attributes.Select(attribute => attribute.AttributeType), Is.EqualTo(new[] { typeof(ShowIfAttribute), typeof(SerializeField) }));

            // ShowIf(string, object) with "m_setOnAwake" and a string-typed null.
            CustomAttributeData showIf = attributes[0];
            Assert.That(showIf.Constructor.GetParameters().Select(parameter => parameter.ParameterType), Is.EqualTo(new[] { typeof(string), typeof(object) }));
            Assert.That(showIf.ConstructorArguments[0].Value, Is.EqualTo("m_setOnAwake"));
            Assert.That(showIf.ConstructorArguments[1].Value, Is.Null);
            Assert.That(showIf.NamedArguments, Is.Empty);
            Assert.That(attributes[1].ConstructorArguments, Is.Empty);

            // Mono reflection reports this null as object, so read this field's own
            // ShowIf row (by its loaded token) and compare the complete blob.
            Dictionary<string, object> evidence = LoadedAttributeBlobEvidence.ReadBoxedStringNull(field, showIf);
            Assert.That(evidence["raw_encoding_hex"], Is.EqualTo(OriginalShowIfBlob));
        }

        [Test]
        public void ShowIfBlobDecoderRejectsNonStringBoxedNull()
        {
            // Predicate: the boxed argument passes only when its tag is 0x0e (String)
            // AND its value is 0xff (null). Each mutation fails exactly one conjunct.
            Assert.DoesNotThrow(() => LoadedAttributeBlobEvidence.VerifyBoxedStringNull(Bytes(OriginalShowIfBlob), "m_setOnAwake"));
            string typeNull = OriginalShowIfBlob.Replace("0eff0000", "50ff0000");   // System.Type null
            string emptyString = OriginalShowIfBlob.Replace("0eff0000", "0e000000"); // String "", not null
            foreach (string mutated in new[] { typeNull, emptyString })
            {
                InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                    LoadedAttributeBlobEvidence.VerifyBoxedStringNull(Bytes(mutated), "m_setOnAwake"));
                Assert.That(exception.Message, Is.EqualTo("Boxed argument is not a String null."), mutated);
            }
        }

        [Test]
        public void ShowIfBlobReaderReadsEachMembersOwnRow()
        {
            // Identical correct rows on other members do not disturb each other's read.
            foreach (string name in new[] { "m_first", "m_second" })
            {
                FieldInfo field = Field(typeof(OtherShowIfRowsFixture), name);
                CustomAttributeData showIf = ShowIf(field);
                Assert.That(showIf.ConstructorArguments[1].ArgumentType, Is.EqualTo(typeof(object)), name);
                Assert.That(LoadedAttributeBlobEvidence.ReadBoxedStringNull(field, showIf)["raw_encoding_hex"], Is.EqualTo(OriginalShowIfBlob), name);
            }
            FieldInfo first = Field(typeof(OtherShowIfRowsFixture), "m_first");
            FieldInfo third = Field(typeof(OtherShowIfRowsFixture), "m_third");
            Assert.That(LoadedAttributeBlobEvidence.ReadBoxedStringNull(third, ShowIf(third))["raw_encoding_hex"], Is.EqualTo("0100076d5f6f746865720eff0000"));

            // The blob comes from the member's own row: m_first's row ("m_setOnAwake")
            // does not satisfy m_third's reflected argument ("m_other").
            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                LoadedAttributeBlobEvidence.ReadBoxedStringNull(first, ShowIf(third)));
            Assert.That(exception.Message, Is.EqualTo("Blob string differs from loaded Reflection argument."));
        }
    }
}
