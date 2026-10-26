using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    internal static class OriginalBindingRosterAssertions
    {
        internal const string ImageOwner = "UnityEngine.UI::UnityEngine.UI.Image";

        internal static void AssertAssets(IEnumerable<string> oldOwners,
            IEnumerable<(string Path, string Guid, string[] References)> originalRows,
            IEnumerable<string> preparedPaths)
        {
            var originals = originalRows.ToArray();
            var owners = oldOwners.ToArray();
            Assert.AreEqual(42, owners.Length);
            Assert.AreEqual(42, owners.Distinct(StringComparer.Ordinal).Count());
            string ScriptGuid(string owner)
            {
                string[] identity = owner.Split(new[] { "::" }, StringSplitOptions.None);
                Assert.AreEqual(2, identity.Length);
                string path = "Assets/Scripts/" + identity[0] + "/" + identity[1].Replace('.', '/') + ".cs";
                var matches = originals.Where(row => row.Path == path).ToArray();
                Assert.AreEqual(1, matches.Length, "Unique original script row: " + owner);
                Assert.That(matches[0].Guid, Does.Match("^[0-9a-f]{32}$"));
                return matches[0].Guid;
            }
            var oldGuids = new HashSet<string>(owners.Select(ScriptGuid), StringComparer.Ordinal);
            Assert.AreEqual(42, oldGuids.Count);
            string imageGuid = ScriptGuid(ImageOwner);
            Assert.IsFalse(oldGuids.Contains(imageGuid));
            var oldPaths = new HashSet<string>(StringComparer.Ordinal);
            var imagePaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in originals)
            {
                if (row.References == null) continue;
                if (row.References.Any(oldGuids.Contains))
                    Assert.IsTrue(oldPaths.Add(row.Path), "Unique original old-roster asset: " + row.Path);
                if (row.References.Contains(imageGuid))
                    Assert.IsTrue(imagePaths.Add(row.Path), "Unique original Image asset: " + row.Path);
            }
            // Image adds 123 paths to the original 811; shared assets retain their old obligations.
            Assert.AreEqual(811, oldPaths.Count);
            Assert.AreEqual(596, imagePaths.Count);
            Assert.AreEqual(123, imagePaths.Except(oldPaths).Count());
            var expected = new HashSet<string>(oldPaths, StringComparer.Ordinal);
            expected.UnionWith(imagePaths);
            Assert.AreEqual(934, expected.Count);
            var actual = new HashSet<string>(StringComparer.Ordinal);
            foreach (string path in preparedPaths)
                Assert.IsTrue(actual.Add(path), "No duplicate prepared binding asset: " + path);
            Assert.AreEqual(934, actual.Count);
            CollectionAssert.AreEquivalent(expected, actual, "Exact original old-roster and Image asset union.");
        }
    }
}
