using System;
using System.Collections;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectLucid.Tests
{
    // Low-priority UI gaps on owned inactive GameObjects. Components are added
    // with AddComponent and never enabled, so no lifecycle callback runs.
    public sealed class OriginalUILowGapTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

        // MenuPage .ctor 0x060001bf (x86-64 0x1b47a10) stores 1 into
        // m_fadeTransition (+0xe8) before tail-calling GuiPage..ctor. A
        // missing or false initializer fails here.
        [Test]
        public void FadeTransitionDefaultsTrueOnARealComponent()
        {
            var owner = new GameObject("Lucid menu page fade default");
            owner.SetActive(false);
            try
            {
                MenuPage page = owner.AddComponent<MenuPage>();
                Assert.That(typeof(MenuPage).GetField("m_fadeTransition", Instance).GetValue(page), Is.EqualTo(true));
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }

        // HLScrollRectSelection.ScrollToOffset 0x06000591 (x86-64 0x1b680b0)
        // calls MonoBehaviour.StopCoroutine (0x1b68128) before storing null
        // into m_scrollCoroutine (+0x88, 0x1b6812d). On a destroyed host the
        // engine's NotNull receiver check faults first, so the retained
        // iterator survives. Clearing the field before stopping leaves null.
        [Test]
        public void DestroyedHostStopFaultKeepsTheRetainedScrollIterator()
        {
            var scrollOwner = new GameObject("Lucid scroll selection rect", typeof(RectTransform));
            var selectionOwner = new GameObject("Lucid scroll selection host", typeof(RectTransform));
            scrollOwner.SetActive(false);
            selectionOwner.SetActive(false);
            try
            {
                ScrollRect scroll = scrollOwner.AddComponent<ScrollRect>();
                var selection = selectionOwner.AddComponent<HLScrollRectSelection>();
                FieldInfo iteratorField = typeof(HLScrollRectSelection).GetField("m_scrollCoroutine", Instance);
                typeof(HLScrollRectSelection).GetField("m_scrollRect", Instance).SetValue(selection, scroll);
                IEnumerator retained = RetainedIterator();
                iteratorField.SetValue(selection, retained);
                UnityEngine.Object.DestroyImmediate(selection);
                MethodInfo scrollToOffset = typeof(HLScrollRectSelection).GetMethod("ScrollToOffset", Instance);
                var failure = Assert.Throws<TargetInvocationException>(() =>
                    scrollToOffset.Invoke(selection, new object[] { Vector2.one, true, false, null }));
                Assert.That(failure.InnerException, Is.Not.Null, "the engine stop on a destroyed host faults");
                Assert.That(iteratorField.GetValue(selection), Is.SameAs(retained));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(selectionOwner);
                UnityEngine.Object.DestroyImmediate(scrollOwner);
            }
        }

        private static IEnumerator RetainedIterator()
        {
            yield break;
        }

        // UIToggleGroup.ValidateToggleIsInGroup 0x06000092 (x86-64 0x1a3e600)
        // formats string literal '{0} {1} is not part of ToggleGroup {2}'
        // (0x1a3e685) with 'UIToggleGroupElement' (0x1a3e694), the toggle and
        // the group, then throws ArgumentException (0x1a3e6d6). A null toggle
        // formats as empty. Altered wording or argument order fails.
        [Test]
        public void NullToggleNotificationThrowsTheExactOriginalMessage()
        {
            var owner = new GameObject("Lucid toggle group message", typeof(RectTransform));
            owner.SetActive(false);
            try
            {
                UIToggleGroup group = owner.AddComponent<UIToggleGroup>();
                var failure = Assert.Throws<ArgumentException>(() => group.NotifyToggleOn(null));
                Assert.That(failure.Message, Is.EqualTo("UIToggleGroupElement  is not part of ToggleGroup " + group.ToString()));
                Assert.That(failure.ParamName, Is.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }
    }
}
