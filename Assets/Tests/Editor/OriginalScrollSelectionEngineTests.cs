using System;
using System.Collections;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class OriginalScrollSelectionEngineTests
    {
        [Test] public void OriginalScrollSelectionActualConstructorsPoliciesAndNullSelection()
            => Assert.AreEqual(34, Verification.OriginalScrollSelectionEngineVerification.OriginalRealConstructorsPoliciesAndNullSelection());
        [Test] public void OriginalScrollSelectionActualSignedGeometryAndAlignmentCases()
            => Assert.AreEqual(41, Verification.OriginalScrollSelectionEngineVerification.OriginalSignedGeometryAndAllAlignmentCases());
        [Test] public void OriginalScrollSelectionActualInstantCallbackAndPartialFailureOrder()
            => Assert.AreEqual(21, Verification.OriginalScrollSelectionEngineVerification.OriginalInstantCallbackAndPartialFailureOrder());

        // MoveScrollRect 0x06000598, iterator MoveNext 0x060005ab x86-64 0x1b698e0:
        // any ordered nonzero difference stores x direction +1 and y direction -1
        // (zero/NaN select the opposite), whatever the sign. A negative x and a
        // positive y difference therefore step away from the target and clamp to
        // it on the first frame. A sign-following direction would step towards
        // the target and stop short of it here.
        [Test]
        public void OriginalSpeedScrollUsesFixedDirectionsAndClampsOppositeSignsToTarget()
        {
            GameObject root = null;
            try
            {
                root = new GameObject("ProjectLucid.ScrollSelection.OwnedSpeedIterator", typeof(RectTransform));
                root.SetActive(false);
                ScrollRect scroll = root.AddComponent<ScrollRect>();
                HLScrollRectSelection selection = root.AddComponent<HLScrollRectSelection>();
                RectTransform viewport = Rect("Viewport", root.transform, new Vector2(200f, 100f));
                scroll.viewport = viewport;
                scroll.content = Rect("Content", viewport, new Vector2(400f, 300f));
                scroll.movementType = ScrollRect.MovementType.Unrestricted;
                using (var serialized = new SerializedObject(selection))
                {
                    serialized.Update();
                    serialized.FindProperty("m_scrollRect").objectReferenceValue = scroll;
                    serialized.FindProperty("m_scrollSpeedPerSecond").floatValue = 1f;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                Assert.That(root.activeInHierarchy, Is.False);
                object frameEnd = typeof(HLScrollRectSelection).GetField("m_waitForFrameEnd",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(selection);

                // Opposite signs: x falls 0.9 -> 0.1 and y rises 0.2 -> 0.8.
                scroll.normalizedPosition = new Vector2(0.9f, 0.2f);
                int finished = 0;
                IEnumerator move = MoveScrollRect(selection, new Vector2(0.1f, 0.8f), () => ++finished);
                Assert.That(move.MoveNext(), Is.True);
                Assert.That(ReferenceEquals(move.Current, frameEnd), Is.True);
                Near(scroll.normalizedPosition, new Vector2(0.1f, 0.8f));
                Assert.That(finished, Is.Zero);
                Assert.That(move.MoveNext(), Is.False);
                Assert.That(finished, Is.EqualTo(1));
                Near(scroll.normalizedPosition, new Vector2(0.1f, 0.8f));

                // Matching signs: one deltaTime*speed step, x up and y down.
                scroll.normalizedPosition = new Vector2(0.1f, 0.8f);
                Vector2 start = scroll.normalizedPosition;
                move = MoveScrollRect(selection, new Vector2(0.9f, 0.2f), null);
                float step = Time.deltaTime * 1f;
                Assert.That(move.MoveNext(), Is.True);
                Vector2 expected = new Vector2(Mathf.Min(start.x + step, 0.9f), Mathf.Max(start.y - step, 0.2f));
                Near(scroll.normalizedPosition, expected);
            }
            finally { if (root != null) UnityEngine.Object.DestroyImmediate(root); }
        }

        private static IEnumerator MoveScrollRect(HLScrollRectSelection selection, Vector2 target, Action finished)
        {
            MethodInfo method = typeof(HLScrollRectSelection).GetMethod("MoveScrollRect", BindingFlags.Instance | BindingFlags.NonPublic);
            // Only constructs the natural iterator; the test drives MoveNext itself.
            return (IEnumerator)method.Invoke(selection, new object[] { target, true, finished });
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            return rect;
        }

        // ScrollRect skips content moves under 0.01 px; 200 px of hidden length
        // bounds that rounding well inside this normalized tolerance.
        private static void Near(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(1e-3f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(1e-3f));
        }
    }
}
