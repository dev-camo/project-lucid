using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Element = ProjectLucid.Tests.OriginalRecyclingScrollRectElementProbe;
using Object = UnityEngine.Object;
using Probe = ProjectLucid.Tests.OriginalRecyclingScrollRectProbe;

namespace ProjectLucid.Tests
{
    // The size-change path of Hardlight.HLRecyclingScrollRect`2 in an active hierarchy,
    // where SetupAtEndOfFrame really starts its coroutine: OnRectTransformDimensionsChange
    // 0x06000661 (0x13937F0) and MarkDirty 0x06000672 (0x13947E0). The probe has no data,
    // so each setup only resizes, waits and calls ContentReady. The probe's HLScrollRect
    // stays disabled; the root is destroyed in TearDown.
    [TestFixture]
    public sealed class OriginalHLRecyclingScrollRectPlayTests
    {
        readonly List<GameObject> roots = new List<GameObject>();

        [TearDown]
        public void DestroyOwnedRoots()
        {
            foreach (GameObject root in roots)
                if (root != null) Object.DestroyImmediate(root);
            roots.Clear();
        }

        static RectTransform NewRect(string name, Transform parent, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            return rect;
        }

        // Start 10, end 6, element 40, spacing 5, viewport 100, allowance 20 (the default),
        // built on an inactive root.
        Probe Build(out GameObject root)
        {
            root = new GameObject("ProjectLucid.HLRecyclingScrollRect.Play.Owned", typeof(RectTransform));
            roots.Add(root);
            root.SetActive(false);
            Probe probe = root.AddComponent<Probe>();
            probe.Content = NewRect("Content", root.transform, new Vector2(100f, 100f));
            probe.Prefab = NewRect("Row", root.transform, new Vector2(40f, 40f)).gameObject.AddComponent<Element>();
            probe.StartPaddingValue = 10f;
            probe.EndPaddingValue = 6f;
            probe.ElementSizeValue = 40f;
            probe.Spacing = 5f;
            probe.ViewportSizeValue = 100f;
            HLScrollRect scroll = root.AddComponent<HLScrollRect>();
            scroll.enabled = false;
            RectTransform viewport = NewRect("Viewport", root.transform, new Vector2(100f, 100f));
            scroll.viewport = viewport;
            scroll.content = NewRect("ScrollContent", viewport, new Vector2(100f, 300f));
            probe.Scroll = scroll;
            return probe;
        }

        static int ContentReadyCount(Probe probe) => probe.Calls.Count(c => c == "ContentReady");

        // 0x1393804 gates MarkDirty on m_regenerateOnSizeChanged. MarkDirty returns at
        // 0x13947e6 while m_isDirty is set and sets it at 0x1394811 after a started setup;
        // StartCoroutine runs the resize (one ContentSize write) before returning, and the
        // finished coroutine clears the flag.
        [UnityTest]
        public IEnumerator SizeChangesMarkDirtyOnlyWhenRegeneratingAndOnlyOnce()
        {
            Probe probe = Build(out GameObject root);
            root.SetActive(true);
            int writes = probe.ContentSizeWrites.Count;
            Assert.That(writes, Is.EqualTo(1), "OnEnable starts one setup");
            for (int frame = 0; frame < 4 && ContentReadyCount(probe) == 0; frame++)
                yield return null;
            Assert.That(ContentReadyCount(probe), Is.EqualTo(1));
            Assert.That(probe.IsDirty, Is.False);

            probe.RunOnRectTransformDimensionsChange();
            Assert.That(probe.IsDirty, Is.False, "regeneration is off");
            Assert.That(probe.ContentSizeWrites.Count, Is.EqualTo(writes));

            probe.RegenerateOnSizeChanged = true;
            probe.RunOnRectTransformDimensionsChange();
            Assert.That(probe.IsDirty, Is.True);
            Assert.That(probe.ContentSizeWrites.Count, Is.EqualTo(writes + 1));
            probe.RunOnRectTransformDimensionsChange();
            Assert.That(probe.ContentSizeWrites.Count, Is.EqualTo(writes + 1), "a dirty rect starts no second setup");

            for (int frame = 0; frame < 4 && probe.IsDirty; frame++)
                yield return null;
            Assert.That(probe.IsDirty, Is.False);
            Assert.That(ContentReadyCount(probe), Is.EqualTo(2));
            Assert.That(probe.ContentSizeWrites.Count, Is.EqualTo(writes + 1));
        }
    }
}
