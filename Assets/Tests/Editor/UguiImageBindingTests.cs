#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ProjectLucid.Tests
{
    public sealed class UguiImageBindingTests
    {
        [Test]
        public void AllAuthoredImagesRetainValuesAndAssetIdentityAcrossRoundtrip()
        {
            Assert.That(UguiImageBindingVerification.RunAuthored(), Is.EqualTo(11228));
        }

        [Test]
        public void OriginalCullEventAncestryRetainsCompleteOrderedDeclarations()
        {
            UguiImageBindingVerification.CheckEventDeclaration();
        }

        [Test]
        public void KnownEventAttributeOrdersRejectUnknownPermutationsAndValues()
        {
            const string serialize = "UnityEngine.CoreModule:UnityEngine.SerializeField()";
            Func<string, string> formerly = value => "UnityEngine.CoreModule:UnityEngine.Serialization.FormerlySerializedAsAttribute(System.String:" + value + ")";
            // Independently observed original orders; pinned Editor reverses exactly
            // these arrays. The original literals remain untouched by validation.
            var cases = new[]
            {
                Tuple.Create("ArgumentCache", "m_ObjectArgument", new[] { formerly("objectArgument"), serialize }),
                Tuple.Create("ArgumentCache", "m_IntArgument", new[] { serialize, formerly("intArgument") }),
                Tuple.Create("ArgumentCache", "m_FloatArgument", new[] { serialize, formerly("floatArgument") }),
                Tuple.Create("PersistentCall", "m_Target", new[] { formerly("instance"), serialize }),
                Tuple.Create("PersistentCall", "m_Arguments", new[] { formerly("arguments"), serialize }),
                Tuple.Create("PersistentCall", "m_CallState", new[] { serialize, formerly("m_Enabled"), formerly("enabled") }),
                Tuple.Create("PersistentCallGroup", "m_Calls", new[] { serialize, formerly("m_Listeners") }),
                Tuple.Create("UnityEventBase", "m_PersistentCalls", new[] { serialize, formerly("m_PersistentListeners") })
            };
            foreach (var row in cases)
            {
                string[] original = row.Item3.ToArray(), loaded = row.Item3.Reverse().ToArray();
                UguiImageBindingVerification.CheckEventAttributeOrder("UnityEngine.Events." + row.Item1, row.Item2, original, loaded);
                CollectionAssert.AreEqual(row.Item3, original);
                var malformed = new List<string[]> { original, loaded.Skip(1).ToArray(), loaded.Concat(new[] { serialize }).ToArray() };
                string[] wrongScope = loaded.ToArray(); wrongScope[0] = wrongScope[0].Replace("UnityEngine.CoreModule:", "Other:");
                malformed.Add(wrongScope);
                string[] wrongValue = loaded.ToArray(); wrongValue[0] += "changed";
                malformed.Add(wrongValue);
                if (loaded.Length == 3) malformed.Add(new[] { original[1], original[2], original[0] });
                foreach (string[] candidate in malformed)
                    Assert.Throws<InvalidOperationException>(() => UguiImageBindingVerification.CheckEventAttributeOrder(
                        "UnityEngine.Events." + row.Item1, row.Item2, original, candidate));
            }
        }

        [Test]
        public void UnlistedEventFieldsStillRequireExactOriginalAttributeOrder()
        {
            string[] original = { "UnityEngine.CoreModule:UnityEngine.SerializeField()", "UnityEngine.CoreModule:UnityEngine.Serialization.FormerlySerializedAsAttribute(System.String:old)" };
            UguiImageBindingVerification.CheckEventAttributeOrder("Unlisted", "field", original, original);
            Assert.Throws<InvalidOperationException>(() => UguiImageBindingVerification.CheckEventAttributeOrder("Unlisted", "field", original, original.Reverse().ToArray()));
        }

        [TestCase("row")]
        [TestCase("count")]
        [TestCase("nested")]
        public void NonemptyBorrowedCollectionSnapshotDetectsMutationWithoutRepair(string mutation)
        {
            object first = new object(), second = new object();
            var nested = new List<object> { first, second };
            var rows = new Dictionary<string, object> { { "borrowed", nested }, { "unrelated", second } };
            Action verify = UguiImageBindingVerification.CaptureBorrowedCollection(rows);
            Assert.DoesNotThrow(() => verify(), "generic dictionary enumerates real DictionaryEntry rows");
            if (mutation == "row") rows["unrelated"] = first;
            else if (mutation == "count") rows.Add("extra", first);
            else nested[0] = second;
            Assert.Throws<InvalidOperationException>(() => verify());
            if (mutation == "row") Assert.That(rows["unrelated"], Is.SameAs(first));
            else if (mutation == "count") Assert.That(rows.ContainsKey("extra"), Is.True);
            else Assert.That(nested[0], Is.SameAs(second));
        }

        [TestCase("folder", false)]
        [TestCase("folder", true)]
        [TestCase("setup", false)]
        [TestCase("setup", true)]
        [TestCase("body", false)]
        [TestCase("body", true)]
        [TestCase("none", false)]
        [TestCase("none", true)]
        public void OwnedImageCleanupAttemptsEveryReleaseAndPreservesFirstFailure(string failureStage, bool failCleanup)
        {
            var primary = new InvalidOperationException("owned Image fixture primary");
            var cleanup = new InvalidOperationException("owned Image fixture cleanup");
            var releases = new List<string>();
            UguiImageBindingVerification.OwnedFixture captured = null;
            GameObject construction = null, contents = null;
            // This real authored material is borrowed; no replacement material or shader.
            string materialPath = AssetDatabase.GUIDToAssetPath("fd67a78bfddedd942bef68ee6c7ef874");
            Material borrowed = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Assert.That(borrowed, Is.Not.Null);
            bool dirty = EditorUtility.IsDirty(borrowed);
            Exception observed = null;
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                try
                {
                    UguiImageBindingVerification.RunOwnedFixture(state =>
                    {
                        captured = state;
                        state.Create(() => { if (failureStage == "folder") throw primary; });
                        construction = state.Construction;
                        UnityEngine.UI.Image image = state.AddImage("Owned image");
                        image.material = borrowed;
                        if (failureStage == "setup") throw primary;
                        state.Save();
                        state.Load();
                        contents = state.Contents;
                        if (failureStage == "body") throw primary;
                    }, stage =>
                    {
                        releases.Add(stage);
                        if (failCleanup && stage == "construction") throw cleanup;
                    });
                }
                catch (Exception error) { observed = error; }
                // Observe the actual boundary before the outer attempt-all recovery.
                if (failureStage == "none" && !failCleanup) Assert.That(observed, Is.Null);
                else Assert.That(observed, Is.SameAs(failureStage == "none" ? cleanup : primary));
                CollectionAssert.AreEqual(new[] { "construction", "contents", "folder" }, releases);
                Assert.That(construction == null, Is.True, "owned construction destroyed");
                Assert.That(contents == null, Is.True, "owned loaded contents unloaded");
                Assert.That(captured, Is.Not.Null);
                Assert.That(AssetDatabase.IsValidFolder(captured.Folder), Is.False);
                Assert.That(borrowed != null, Is.True);
                Assert.That(AssetDatabase.LoadAssetAtPath<Material>(materialPath), Is.SameAs(borrowed));
                Assert.That(EditorUtility.IsDirty(borrowed), Is.EqualTo(dirty));
            }, () => captured?.Dispose()); // recovery follows every boundary assertion
        }
    }
}
#endif
