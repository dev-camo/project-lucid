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
        [Test]
        public void AllowExitToHomeRetainsOriginalOrderedAttributes() // field token 0x0400000f
        {
            FieldInfo field = typeof(AppleTVRemoteSetter).GetField("m_allowExitToHome", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Assert.That(field, Is.Not.Null);
            CustomAttributeData[] attributes = field.GetCustomAttributesData().ToArray();
            Assert.That(attributes.Select(attribute => attribute.AttributeType), Is.EqualTo(new[] { typeof(ShowIfAttribute), typeof(SerializeField) }));

            // ShowIf(string, object) with "m_setOnAwake" and a string-typed null.
            CustomAttributeData showIf = attributes[0];
            Assert.That(showIf.Constructor.GetParameters().Select(parameter => parameter.ParameterType), Is.EqualTo(new[] { typeof(string), typeof(object) }));
            Assert.That(showIf.ConstructorArguments[0].Value, Is.EqualTo("m_setOnAwake"));
            Assert.That(showIf.ConstructorArguments[1].ArgumentType, Is.EqualTo(typeof(string)));
            Assert.That(showIf.ConstructorArguments[1].Value, Is.Null);
            Assert.That(showIf.NamedArguments, Is.Empty);
            Assert.That(attributes[1].ConstructorArguments, Is.Empty);
        }
    }
}
