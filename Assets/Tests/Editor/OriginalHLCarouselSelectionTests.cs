using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour of Hardlight.HLCarouselSelection (HLUnityUI.Runtime 0x06000560..564)
    // against the shipped x86-64 bodies, and its effect on the inherited
    // HLScrollRectSelection.CalculateTargetScrollOffset. Each case owns an inactive
    // HideAndDontSave root that TearDown destroys; no shared state is touched.
    [TestFixture]
    public sealed class OriginalHLCarouselSelectionTests
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        readonly List<GameObject> roots = new List<GameObject>();

        [TearDown]
        public void DestroyOwnedRoots()
        {
            foreach (GameObject root in roots)
                if (root != null) Object.DestroyImmediate(root);
            roots.Clear();
        }

        static object Property(object target, string name) =>
            typeof(HLScrollRectSelection).GetProperty(name, Any).GetValue(target);

        [Test]
        public void DeclarationOverridesTheFourSelectionPolicies()
        {
            Type type = typeof(HLCarouselSelection);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.BaseType, Is.EqualTo(typeof(HLScrollRectSelection)));
            Assert.That(type.IsSealed || type.IsAbstract, Is.False);
            const BindingFlags declared = Any | BindingFlags.Static | BindingFlags.DeclaredOnly;
            Assert.That(type.GetFields(declared), Is.Empty);
            Assert.That(type.GetProperties(declared).Select(p => p.Name).ToArray(),
                Is.EqualTo(new[] { "NormaliseWhenNegativeOne", "ClampTargetScrollOffset", "ShouldWrap", "BookendOffset" }));
            foreach (PropertyInfo property in type.GetProperties(declared))
            {
                MethodInfo getter = property.GetGetMethod(true);
                Assert.That(getter.IsFamily && getter.IsVirtual && getter.GetBaseDefinition().DeclaringType == typeof(HLScrollRectSelection),
                    Is.True, property.Name);
                Assert.That(property.CanWrite, Is.False, property.Name);
            }
        }

        // 0x1B660A0..0D0: false, false, true and the rodata float 0.1 at 0x29a6bfc.
        // The base class values are the controls (true, true, false, 0).
        [Test]
        public void PoliciesDisableNormalisingAndClampingEnableWrappingAndWidenTheBookends()
        {
            var root = new GameObject("ProjectLucid.HLCarouselSelection.Owned") { hideFlags = HideFlags.HideAndDontSave };
            roots.Add(root);
            root.SetActive(false);
            var carousel = root.AddComponent<HLCarouselSelection>();
            var plain = root.AddComponent<HLScrollRectSelection>();

            Assert.That(Property(carousel, "NormaliseWhenNegativeOne"), Is.False);
            Assert.That(Property(carousel, "ClampTargetScrollOffset"), Is.False);
            Assert.That(Property(carousel, "ShouldWrap"), Is.True);
            Assert.That(Property(carousel, "BookendOffset"), Is.EqualTo(0.1f));
            Assert.That(Property(plain, "NormaliseWhenNegativeOne"), Is.True);
            Assert.That(Property(plain, "ClampTargetScrollOffset"), Is.True);
            Assert.That(Property(plain, "ShouldWrap"), Is.False);
            Assert.That(Property(plain, "BookendOffset"), Is.EqualTo(0f));
        }

        // HLScrollRectSelection 0x06000599 divides (listStart - selectionStart) by
        // (listExtent - viewportExtent) and clamps only through slot 7. A 400 unit
        // list (-200..200) with a -100 viewport extent gives -300; a selection
        // starting at 200 gives 4/3 and one starting at -260 gives -0.2.
        [Test]
        public void TargetScrollOffsetPastTheListEndIsUnclampedOnlyForTheCarousel()
        {
            Assert.That(TargetOffset(true, 220f), Is.EqualTo(4f / 3f).Within(1e-4f));
            Assert.That(TargetOffset(false, 220f), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void TargetScrollOffsetBeforeTheListStartIsUnclampedOnlyForTheCarousel()
        {
            Assert.That(TargetOffset(true, -240f), Is.EqualTo(-0.2f).Within(1e-4f));
            Assert.That(TargetOffset(false, -240f), Is.EqualTo(0f).Within(1e-4f));
        }

        float TargetOffset(bool carousel, float selectionX)
        {
            var root = new GameObject("ProjectLucid.HLCarouselSelection.Owned", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
            roots.Add(root);
            root.SetActive(false);
            HLScrollRectSelection selection = carousel ? root.AddComponent<HLCarouselSelection>() : root.AddComponent<HLScrollRectSelection>();
            RectTransform list = NewRect("List", root.transform, new Vector2(400f, 50f));
            RectTransform item = NewRect("Item", list, new Vector2(40f, 20f));
            item.anchoredPosition = new Vector2(selectionX, 0f);

            Type type = typeof(HLScrollRectSelection);
            type.GetField("m_listContents", Any).SetValue(selection, list.gameObject);
            FieldInfo direction = type.GetField("m_direction", Any);
            direction.SetValue(selection, Enum.ToObject(direction.FieldType, (int)HLScrollRectSelection.Direction.Horizontal));
            type.GetField("m_viewportTransformExtent", Any).SetValue(selection, -100f);
            return (float)type.GetMethod("CalculateTargetScrollOffset", Any)
                .Invoke(selection, new object[] { item, HLScrollRectSelection.Alignment.TopOrLeft, false });
        }

        static RectTransform NewRect(string name, Transform parent, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.gameObject.hideFlags = HideFlags.HideAndDontSave;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            return rect;
        }
    }
}
