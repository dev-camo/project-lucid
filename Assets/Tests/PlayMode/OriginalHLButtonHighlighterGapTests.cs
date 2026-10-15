using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Source = HLButtonHighlightCustomisations.ButtonHighlightChangeSource;
using Targets = Hardlight.HLButtonHighlighter.ButtonHighlightChangeTargets;
using UObject = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Owned GameObjects with genuine HLButtonHighlighter/Animation components and
    // test-only recording ModifiedOnUISelection components. No shared static,
    // registry or authored asset is touched.
    public sealed class OriginalHLButtonHighlighterGapTests
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // ButtonHighlightChangeTargets 0x06000438..0x0600043c (x86-64 0x1b5b5d0
        // Deactivate, 0x1b5b630 Activate, 0x1b5b690 Begin, 0x1b5b6f0 Updated,
        // 0x1b5b770 End) each loop over the array, calling the same-named
        // virtual with the arguments in order, and reload the array field
        // (+0x10) after every call. Swapping Activate/Deactivate, swapping the
        // source arguments or caching the array in a local fails below.
        [Test]
        public void TargetsForwardEachCallbackInOrderAndRereadTheArray()
        {
            var owned = new List<UObject>();
            try
            {
                var owner = new GameObject("Lucid owned recording modifications");
                owned.Add(owner);
                owner.SetActive(false);
                var sink = new List<string>();
                Source previous = new Source(), current = new Source();
                Func<Source, string> describe = s => ReferenceEquals(s, previous) ? "prev" : ReferenceEquals(s, current) ? "cur" : "?";
                PlayModeRecordingSelectionModification a = Recorder(owner, "a", sink, describe);
                PlayModeRecordingSelectionModification b = Recorder(owner, "b", sink, describe);
                PlayModeRecordingSelectionModification c = Recorder(owner, "c", sink, describe);
                var targets = new Targets { UISelectionModifications = new ModifiedOnUISelection[] { a, b } };

                targets.Deactivate(current);
                targets.Activate(current);
                targets.OnHighlightBegin(previous, current);
                targets.OnHighlightUpdated(previous, current, 0.25f);
                targets.OnHighlightEnd(previous, current);
                Assert.That(sink, Is.EqualTo(new[] {
                    "a Deactivate cur", "b Deactivate cur",
                    "a Activate cur", "b Activate cur",
                    "a Begin prev cur", "b Begin prev cur",
                    "a Updated prev cur 0.25", "b Updated prev cur 0.25",
                    "a End prev cur", "b End prev cur" }));

                // The first callback replaces the array; the loop continues on the new one.
                var swaps = new Dictionary<string, Action<Targets>>
                {
                    { "Deactivate", t => t.Deactivate(current) },
                    { "Activate", t => t.Activate(current) },
                    { "Begin", t => t.OnHighlightBegin(previous, current) },
                    { "Updated", t => t.OnHighlightUpdated(previous, current, 1f) },
                    { "End", t => t.OnHighlightEnd(previous, current) },
                };
                foreach (KeyValuePair<string, Action<Targets>> swap in swaps)
                {
                    sink.Clear();
                    targets.UISelectionModifications = new ModifiedOnUISelection[] { a, b };
                    a.AfterCall = entry => targets.UISelectionModifications = new ModifiedOnUISelection[] { a, c, b };
                    swap.Value(targets);
                    a.AfterCall = null;
                    Assert.That(sink.ConvertAll(entry => entry.Substring(0, 1)), Is.EqualTo(new[] { "a", "c", "b" }), swap.Key);
                }
            }
            finally
            {
                for (int i = owned.Count - 1; i >= 0; --i)
                    if (owned[i] != null) UObject.DestroyImmediate(owned[i]);
            }
        }

        // SetButtonState 0x06000430 (x86-64 0x1b59a30) starts the Apply iterator
        // only when activeSelf (0x1b59b36) and activeInHierarchy (0x1b59b52) both
        // hold; Apply never yields, so its callbacks run inside the call. Before
        // starting it, a non-null m_coroutine (+0x60) is passed to
        // SafeStopCoroutine (0x1b59b72) and the new handle is stored. Since the
        // genuine iterator always completes synchronously, a stored handle that
        // is still running is reachable only through this test-owned stand-in
        // coroutine on the same host. Skipping the stop lets it resume; dropping
        // the activeInHierarchy test starts a coroutine on an inactive object.
        [UnityTest]
        public IEnumerator ActiveSetButtonStateStopsStoredCoroutineBeforeApplyingSynchronously()
        {
            var owned = new List<UObject>();
            try
            {
                var parent = new GameObject("Lucid owned highlighter parent");
                owned.Add(parent);
                parent.SetActive(false);
                var root = new GameObject("Lucid owned active highlighter", typeof(RectTransform));
                owned.Add(root);
                root.transform.SetParent(parent.transform, false);
                HLButtonHighlighter highlighter = root.AddComponent<HLButtonHighlighter>();
                Animation animation = root.AddComponent<Animation>();
                var sink = new List<string>();
                var names = new Dictionary<Source, string>();
                PlayModeRecordingSelectionModification layer = Recorder(root, "m", sink, s => s != null && names.TryGetValue(s, out string n) ? n : "?");
                Set(highlighter, "m_customisableAnimator", animation);
                Set(highlighter, "m_customisableLayers", new List<Targets> { new Targets { UISelectionModifications = new ModifiedOnUISelection[] { layer } } });

                // activeSelf without activeInHierarchy: lists rotate, nothing starts.
                highlighter.SetButtonState(Asset(owned, names, "a0"), false);
                Assert.That(root.activeSelf && !root.activeInHierarchy, Is.True);
                Assert.That(sink, Is.Empty);
                Assert.That(Get(highlighter, "m_coroutine"), Is.Null);

                parent.SetActive(true);
                highlighter.SetButtonState(Asset(owned, names, "b0"), false);
                Assert.That(sink, Is.EqualTo(new[] { "m Begin a0 b0", "m Updated a0 b0 1", "m End a0 b0" }), "applied inside the call");

                int resumed = 0;
                Coroutine pending = highlighter.StartCoroutine(Pending(() => ++resumed));
                Assert.That(resumed, Is.EqualTo(1));
                Set(highlighter, "m_coroutine", pending);
                sink.Clear();
                highlighter.SetButtonState(Asset(owned, names, "c0"), false);
                Assert.That(sink, Is.EqualTo(new[] { "m Begin b0 c0", "m Updated b0 c0 1", "m End b0 c0" }));
                Assert.That(ReferenceEquals(Get(highlighter, "m_coroutine"), pending), Is.False);
                yield return null;
                yield return null;
                Assert.That(resumed, Is.EqualTo(1), "stored coroutine was stopped");
            }
            finally
            {
                for (int i = owned.Count - 1; i >= 0; --i)
                    if (owned[i] != null) UObject.DestroyImmediate(owned[i]);
            }
        }

        private static IEnumerator Pending(Action step)
        {
            while (true)
            {
                step();
                yield return null;
            }
        }

        private static PlayModeRecordingSelectionModification Recorder(GameObject owner, string label, List<string> sink, Func<Source, string> describe)
        {
            PlayModeRecordingSelectionModification recorder = owner.AddComponent<PlayModeRecordingSelectionModification>();
            recorder.Label = label;
            recorder.Sink = sink;
            recorder.Describe = describe;
            return recorder;
        }

        private static HLButtonHighlightCustomisations Asset(List<UObject> owned, Dictionary<Source, string> names, string name)
        {
            var asset = ScriptableObject.CreateInstance<HLButtonHighlightCustomisations>();
            owned.Add(asset);
            var source = new Source();
            names.Add(source, name);
            asset.m_buttonHighlightLayerData = new List<Source> { source };
            return asset;
        }

        private static FieldInfo Field(Type type, string name) =>
            type.GetField(name, AnyInstance | BindingFlags.DeclaredOnly) ?? throw new MissingFieldException(name);

        // Stands in for authored serialized references, and once for a stored handle.
        private static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
        private static object Get(object target, string name) => Field(target.GetType(), name).GetValue(target);
    }
}
