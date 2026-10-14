using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Editor
{
    // Declarations and the marker-name table recovered from the original 1.10.1 static constructor.
    public sealed class OriginalHLProfilerMarkerConverterTests
    {
        [Test]
        public void ConverterRetainsOriginalDeclarations()
        {
            var type = typeof(HLProfilerMarkerConverter);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityCore.Runtime"));
            // Original flags 0x00100181: Public | Abstract | Sealed | BeforeFieldInit.
            Assert.That((int)type.Attributes, Is.EqualTo(0x00100181));
            FieldInfo field = type.GetField("HLProfilerMarkerToName", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field.FieldType, Is.EqualTo(typeof(Dictionary<HLProfilerMarker, string>)));
            // Original field flags 0x31: Private | Static | InitOnly.
            Assert.That((int)field.Attributes, Is.EqualTo(0x31));
        }

        [Test]
        public void TableRetainsOriginalEntriesAndInsertionOrder()
        {
            var table = (Dictionary<HLProfilerMarker, string>)typeof(HLProfilerMarkerConverter)
                .GetField("HLProfilerMarkerToName", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            Assert.That(table.Count, Is.EqualTo(87));
            string canonical = string.Join("\n", table.Select(entry => entry.Key + "\t" + entry.Value));
            using (var sha = SHA256.Create())
                Assert.That(string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical)).Select(b => b.ToString("x2"))),
                    Is.EqualTo("9aaabcba35b365a9f5366ecfd03d191bcfdc6d810b265bd7b3a92a92802c971f"));
            Assert.That(HLProfilerMarkerConverter.GetName(HLProfilerMarker.SystemUsedMemory), Is.EqualTo("System Used Memory"));
            Assert.That(HLProfilerMarkerConverter.GetName(HLProfilerMarker.None), Is.Null);
        }
    }
}
