using System.Collections.Generic;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectLucid.Tests
{
    // Behaviour of the original HLUnityCore.Runtime AnimationExtensions (02000059)
    // and ScrollRectExtensions (02000080) against their 1.10.1 x86-64 bodies.
    // Every GameObject is created and destroyed by this fixture.
    // Recorded EditMode gaps: AnimationExtensions.Stop 0x0600023d's immediate and
    // blend-plus-coroutine paths with WaitUntilBlendIsFinished (a playing legacy
    // clip and CoroutineUtils host need Play Mode), and the order of its early
    // out-parameter write against the guards (0x1aae1d8, before op_Equality),
    // which a caller cannot observe.
    [TestFixture]
    public sealed class OriginalHLUnityCoreAnimationScrollRectExtensionsTests
    {
        private readonly List<GameObject> m_ownedObjects = new List<GameObject>();

        private GameObject Create(string name, Transform parent = null)
        {
            GameObject gameObject = new GameObject(name);
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }
            else
            {
                m_ownedObjects.Add(gameObject);
            }
            return gameObject;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in m_ownedObjects)
            {
                if (gameObject != null)
                {
                    Object.DestroyImmediate(gameObject);
                }
            }
            m_ownedObjects.Clear();
        }

        // ResetScroll 0x06000305 (0x1ab39a0): StopMovement, then horizontal 0 and
        // vertical 1, each only for an enabled axis.
        [Test]
        public void ResetScrollSnapsEnabledAxesToTheirStart()
        {
            GameObject root = Create("Scroll");
            RectTransform viewRect = root.AddComponent<RectTransform>();
            viewRect.sizeDelta = new Vector2(100f, 100f);
            GameObject contentObject = Create("Content", root.transform);
            RectTransform content = contentObject.AddComponent<RectTransform>();
            content.sizeDelta = new Vector2(300f, 300f);
            ScrollRect scrollRect = root.AddComponent<ScrollRect>();
            scrollRect.content = content;

            scrollRect.horizontalNormalizedPosition = 0.5f;
            scrollRect.verticalNormalizedPosition = 0.5f;
            scrollRect.velocity = new Vector2(5f, 5f);
            scrollRect.ResetScroll();
            Assert.That(scrollRect.velocity, Is.EqualTo(Vector2.zero));
            Assert.That(scrollRect.horizontalNormalizedPosition, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(scrollRect.verticalNormalizedPosition, Is.EqualTo(1f).Within(1e-4f));

            scrollRect.horizontal = false;
            scrollRect.horizontalNormalizedPosition = 0.5f;
            scrollRect.verticalNormalizedPosition = 0.5f;
            scrollRect.ResetScroll();
            Assert.That(scrollRect.horizontalNormalizedPosition, Is.EqualTo(0.5f).Within(1e-4f), "a disabled axis is left alone");
            Assert.That(scrollRect.verticalNormalizedPosition, Is.EqualTo(1f).Within(1e-4f));

            // With both axes off only StopMovement runs (the position setters would
            // also zero their axis' velocity, so this isolates the stop).
            scrollRect.vertical = false;
            scrollRect.verticalNormalizedPosition = 0.5f;
            scrollRect.velocity = new Vector2(5f, 5f);
            scrollRect.ResetScroll();
            Assert.That(scrollRect.velocity, Is.EqualTo(Vector2.zero));
            Assert.That(scrollRect.verticalNormalizedPosition, Is.EqualTo(0.5f).Within(1e-4f));
        }

        // AnimationExtensions.Stop 0x0600023d (0x1aae1a0) returns for a missing
        // component or clip; ResetScroll 0x06000305 (0x1ab39a0) guards with the
        // Unity inequality.
        [Test]
        public void AnimationStopAndResetScrollReturnOnMissingTargets()
        {
            GameObject root = Create("Root");
            Animation animation = root.AddComponent<Animation>();
            animation.Stop("Missing", 1f, out Coroutine coroutine);
            Assert.That(coroutine, Is.Null);

            Object.DestroyImmediate(animation);
            Assert.DoesNotThrow(() => animation.Stop("Missing", 1f, out _));
            Assert.DoesNotThrow(() => ((Animation)null).Stop("Missing", 0f, out _));
            Assert.DoesNotThrow(() => ((ScrollRect)null).ResetScroll());
        }
    }
}
