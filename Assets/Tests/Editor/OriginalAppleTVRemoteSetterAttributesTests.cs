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

        // Binding control: same ShowIf(String,Object) constructor, but the boxed
        // null is typed System.Type (element tag 0x50), not String (0x0e).
        private sealed class WrongTypedNullFixture
        {
#pragma warning disable 0169
            [ShowIf("m_setOnAwake", (Type)null)]
            [SerializeField]
            private bool m_allowExitToHome;
#pragma warning restore 0169
        }

        private static FieldInfo Field(Type owner) =>
            owner.GetField("m_allowExitToHome", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

        [Test]
        public void AllowExitToHomeRetainsOriginalOrderedAttributes() // field token 0x0400000f
        {
            FieldInfo field = Field(typeof(AppleTVRemoteSetter));
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
            Assert.That(evidence["member_token"], Is.EqualTo("0x" + field.MetadataToken.ToString("x8")));
            Assert.That(evidence["raw_encoding_hex"], Is.EqualTo(OriginalShowIfBlob));
            Assert.That(evidence["boxed_element_tag"], Is.EqualTo(0x0e));

            // The reader is bound to the inspected member: a differently typed null
            // on the same constructor is rejected even though the target passes.
            FieldInfo control = Field(typeof(WrongTypedNullFixture));
            CustomAttributeData controlShowIf = control.GetCustomAttributesData().Single(attribute => attribute.AttributeType == typeof(ShowIfAttribute));
            Assert.That(controlShowIf.Constructor, Is.EqualTo(showIf.Constructor));
            Assert.That(controlShowIf.ConstructorArguments[1].Value, Is.Null);
            Assert.Throws<InvalidDataException>(() => LoadedAttributeBlobEvidence.ReadBoxedStringNull(control, controlShowIf));
        }
    }
}
