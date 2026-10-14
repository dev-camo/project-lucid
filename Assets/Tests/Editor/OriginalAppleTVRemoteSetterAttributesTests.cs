using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
            Assert.That(showIf.ConstructorArguments[1].Value, Is.Null);
            // Mono reflection reports this null as object, so check the compiled blob:
            // prolog, "m_setOnAwake", string-typed null (0x0E 0xFF), no named arguments.
            byte[] blob = new byte[] { 0x13, 0x01, 0x00, 0x0C }
                .Concat(Encoding.ASCII.GetBytes("m_setOnAwake"))
                .Concat(new byte[] { 0x0E, 0xFF, 0x00, 0x00 }).ToArray();
            byte[] image = File.ReadAllBytes(typeof(AppleTVRemoteSetter).Assembly.Location);
            Assert.That(Occurrences(image, blob), Is.EqualTo(1));
            Assert.That(showIf.NamedArguments, Is.Empty);
            Assert.That(attributes[1].ConstructorArguments, Is.Empty);
        }

        private static int Occurrences(byte[] image, byte[] pattern)
        {
            int count = 0;
            for (int start = 0; start <= image.Length - pattern.Length; ++start)
            {
                int index = 0;
                while (index < pattern.Length && image[start + index] == pattern[index]) ++index;
                if (index == pattern.Length) ++count;
            }
            return count;
        }
    }
}
