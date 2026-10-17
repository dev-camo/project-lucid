using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using Element = ProjectLucid.Tests.OriginalRecyclingScrollRectElementProbe;
using Horizontal = ProjectLucid.Tests.OriginalHorizontalRecyclingScrollRectProbe;
using Object = UnityEngine.Object;
using Vertical = ProjectLucid.Tests.OriginalVerticalRecyclingScrollRectProbe;

namespace ProjectLucid.Tests
{
    // Behaviour of the abstract Hardlight.HLHorizontalRecyclingScrollRect`2
    // (HLUnityUI.Runtime 0x0600063e..646) and HLVerticalRecyclingScrollRect`2
    // (0x060006ff..707) overrides against the fully shared x86-64 bodies, through
    // test-only subclasses that only expose them (no original subclass exists). Every
    // case owns an inactive HideAndDontSave root with a disabled HLScrollRect and
    // destroys it in TearDown. Element rects are deliberately separate from the
    // elements' own transforms, so the cases show that the overrides read the
    // element's RectTransform property. RectTransform values are compared with a small
    // tolerance.
    [TestFixture]
    public sealed class OriginalHLDirectionalRecyclingScrollRectTests
    {
        const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        const float Tolerance = 1e-4f;

        static readonly FieldInfo ElementRectField =
            typeof(HLRecyclingScrollRectElement<int>).GetField("m_rectTransform", BindingFlags.Instance | BindingFlags.NonPublic);

        readonly List<GameObject> roots = new List<GameObject>();

        [TearDown]
        public void DestroyOwnedRoots()
        {
            foreach (GameObject root in roots)
                if (root != null) Object.DestroyImmediate(root);
            roots.Clear();
        }

        sealed class Rig
        {
            public GameObject Root;
            public HLScrollRect Scroll;
            public RectTransform Content;
            public Element Prefab;
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

        // Viewport 120 x 60, scroll content 300 x 300, list content 240 x 80, and a prefab
        // whose own transform is 99 x 99 but whose RectTransform field is 30 x 20.
        Rig Build()
        {
            var rig = new Rig();
            rig.Root = new GameObject("ProjectLucid.DirectionalRecyclingScrollRect.Owned", typeof(RectTransform));
            roots.Add(rig.Root);
            rig.Root.hideFlags = HideFlags.HideAndDontSave;
            rig.Root.SetActive(false);
            rig.Scroll = rig.Root.AddComponent<HLScrollRect>();
            rig.Scroll.enabled = false;
            RectTransform viewport = NewRect("Viewport", rig.Root.transform, new Vector2(120f, 60f));
            rig.Scroll.viewport = viewport;
            rig.Scroll.content = NewRect("ScrollContent", viewport, new Vector2(300f, 300f));
            rig.Content = NewRect("Content", rig.Scroll.content, new Vector2(240f, 80f));
            rig.Prefab = Child(rig, "Row", new Vector2(30f, 20f));
            return rig;
        }

        static Element Child(Rig rig, string name, Vector2 rectSize)
        {
            RectTransform own = NewRect(name, rig.Root.transform, new Vector2(99f, 99f));
            Element element = own.gameObject.AddComponent<Element>();
            ElementRectField.SetValue(element, NewRect(name + "Rect", rig.Root.transform, rectSize));
            return element;
        }

        Horizontal BuildHorizontal(out Rig rig)
        {
            rig = Build();
            Horizontal probe = rig.Root.AddComponent<Horizontal>();
            probe.Scroll = rig.Scroll;
            probe.Content = rig.Content;
            probe.Prefab = rig.Prefab;
            probe.InternalPadding = new RectOffset(1, 2, 3, 4);
            probe.ExternalPadding = new Vector2(3f, 7f);
            return probe;
        }

        Vertical BuildVertical(out Rig rig)
        {
            rig = Build();
            Vertical probe = rig.Root.AddComponent<Vertical>();
            probe.Scroll = rig.Scroll;
            probe.Content = rig.Content;
            probe.Prefab = rig.Prefab;
            probe.InternalPadding = new RectOffset(1, 2, 3, 4);
            probe.ExternalPadding = new Vector2(3f, 7f);
            return probe;
        }

        static void Near(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
        }

        static void AssertDeclaration(Type type, Type alignment)
        {
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.IsAbstract && type.IsPublic, Is.True);
            Type[] parameters = type.GetGenericArguments();
            Assert.That(type.BaseType, Is.EqualTo(typeof(HLRecyclingScrollRect<,>).MakeGenericType(parameters)));
            Assert.That(parameters[0].GetGenericParameterConstraints(),
                Is.EqualTo(new[] { typeof(HLRecyclingScrollRectElement<>).MakeGenericType(parameters[1]) }));
            Assert.That(type.GetCustomAttributesData().Select(a => Convert.ToInt32(a.ConstructorArguments[0].Value)).ToArray(),
                Is.EqualTo(new[] { 2, 1 }));
            FieldInfo field = type.GetFields(Declared).Single();
            Assert.That(field.Name, Is.EqualTo("m_childAlignment"));
            Assert.That(field.FieldType, Is.EqualTo(alignment));
            Assert.That(field.IsFamily && field.IsDefined(typeof(SerializeField)), Is.True);
            Assert.That(type.GetProperties(Declared).Select(p => p.Name).ToArray(),
                Is.EqualTo(new[] { "StartPadding", "EndPadding", "ContentSize", "ViewportSize", "ElementSize" }));
            foreach (MethodInfo method in type.GetMethods(Declared))
                Assert.That(method.IsFamily && method.IsVirtual && method.GetBaseDefinition() != method, Is.True, method.Name);
            Assert.That(type.GetMethods(Declared).Length, Is.EqualTo(8));
            Assert.That(type.GetConstructors(Declared).Single().IsFamily, Is.True);
        }

        // dump.cs TypeDefIndex 12053/12075; both constructors (0x1393120, 0x1394BF0) store
        // alignment 1 (Center) before the base constructor.
        [Test]
        public void DeclarationsKeepTheDumpedAlignmentFieldAndCentreDefault()
        {
            AssertDeclaration(typeof(HLHorizontalRecyclingScrollRect<,>), typeof(VerticalAlignment));
            AssertDeclaration(typeof(HLVerticalRecyclingScrollRect<,>), typeof(HorizontalAlignment));
            Assert.That(BuildHorizontal(out _).ChildAlignment, Is.EqualTo(VerticalAlignment.Center));
            Assert.That(BuildVertical(out _).ChildAlignment, Is.EqualTo(HorizontalAlignment.Center));
        }

        // 0x0600063e..644: left/right internal padding, list content width, viewport width,
        // the prefab's RectTransform width and the x of the normalised position.
        [Test]
        public void HorizontalSizesReadWidthsAndLeftRightPadding()
        {
            Horizontal probe = BuildHorizontal(out _);
            Assert.That(probe.StartPaddingValue, Is.EqualTo(1f));
            Assert.That(probe.EndPaddingValue, Is.EqualTo(2f));
            Assert.That(probe.ContentSizeValue, Is.EqualTo(240f).Within(Tolerance));
            Assert.That(probe.ViewportSizeValue, Is.EqualTo(120f).Within(Tolerance));
            Assert.That(probe.ElementSizeValue, Is.EqualTo(30f).Within(Tolerance));
            Assert.That(probe.RunGetNormalizedPositionOnAxis(new Vector2(0.25f, 0.75f)), Is.EqualTo(0.25f));
        }

        // 0x060006ff..705: top/bottom internal padding, heights and the y of the
        // normalised position.
        [Test]
        public void VerticalSizesReadHeightsAndTopBottomPadding()
        {
            Vertical probe = BuildVertical(out _);
            Assert.That(probe.StartPaddingValue, Is.EqualTo(3f));
            Assert.That(probe.EndPaddingValue, Is.EqualTo(4f));
            Assert.That(probe.ContentSizeValue, Is.EqualTo(80f).Within(Tolerance));
            Assert.That(probe.ViewportSizeValue, Is.EqualTo(60f).Within(Tolerance));
            Assert.That(probe.ElementSizeValue, Is.EqualTo(20f).Within(Tolerance));
            Assert.That(probe.RunGetNormalizedPositionOnAxis(new Vector2(0.25f, 0.75f)), Is.EqualTo(0.75f));
        }

        // 0x06000641/0x06000702: the scroll rect's content gets both external paddings on
        // top of the value, the list content gets the value; the other axis is untouched.
        [Test]
        public void ContentSizeSetterSizesBothContentsAlongTheAxis()
        {
            Horizontal horizontal = BuildHorizontal(out Rig horizontalRig);
            horizontal.ContentSizeValue = 200f;
            Assert.That(horizontalRig.Scroll.content.rect.size.x, Is.EqualTo(210f).Within(Tolerance));
            Assert.That(horizontalRig.Scroll.content.rect.size.y, Is.EqualTo(300f).Within(Tolerance));
            Assert.That(horizontalRig.Content.rect.size.x, Is.EqualTo(200f).Within(Tolerance));
            Assert.That(horizontalRig.Content.rect.size.y, Is.EqualTo(80f).Within(Tolerance));

            Vertical vertical = BuildVertical(out Rig verticalRig);
            vertical.ContentSizeValue = 200f;
            Assert.That(verticalRig.Scroll.content.rect.size.x, Is.EqualTo(300f).Within(Tolerance));
            Assert.That(verticalRig.Scroll.content.rect.size.y, Is.EqualTo(210f).Within(Tolerance));
            Assert.That(verticalRig.Content.rect.size.x, Is.EqualTo(240f).Within(Tolerance));
            Assert.That(verticalRig.Content.rect.size.y, Is.EqualTo(200f).Within(Tolerance));
        }

        // 0x06000645: x is the position; y is -top, 0 or +bottom by alignment, and an
        // undefined alignment writes nothing.
        [Test]
        public void HorizontalChildPositionUsesTheVerticalAlignment()
        {
            Horizontal probe = BuildHorizontal(out Rig rig);
            Element child = Child(rig, "Child", new Vector2(10f, 10f));
            RectTransform target = child.RectTransform;
            ((RectTransform)child.transform).anchoredPosition = new Vector2(9f, 9f);

            probe.ChildAlignment = VerticalAlignment.Top;
            probe.RunSetChildPosition(child, 12.5f);
            Near(target.anchoredPosition, new Vector2(12.5f, -3f));
            probe.ChildAlignment = VerticalAlignment.Center;
            probe.RunSetChildPosition(child, 12.5f);
            Near(target.anchoredPosition, new Vector2(12.5f, 0f));
            probe.ChildAlignment = VerticalAlignment.Bottom;
            probe.RunSetChildPosition(child, 12.5f);
            Near(target.anchoredPosition, new Vector2(12.5f, 4f));
            probe.ChildAlignment = (VerticalAlignment)3;
            probe.RunSetChildPosition(child, 99f);
            Near(target.anchoredPosition, new Vector2(12.5f, 4f));
            Near(((RectTransform)child.transform).anchoredPosition, new Vector2(9f, 9f));
        }

        // 0x06000706: y is the negated position; x is +left, 0 or -right by alignment, and
        // an undefined alignment writes nothing.
        [Test]
        public void VerticalChildPositionNegatesThePositionAndUsesTheHorizontalAlignment()
        {
            Vertical probe = BuildVertical(out Rig rig);
            Element child = Child(rig, "Child", new Vector2(10f, 10f));
            RectTransform target = child.RectTransform;
            ((RectTransform)child.transform).anchoredPosition = new Vector2(9f, 9f);

            probe.ChildAlignment = HorizontalAlignment.Left;
            probe.RunSetChildPosition(child, 12.5f);
            Near(target.anchoredPosition, new Vector2(1f, -12.5f));
            probe.ChildAlignment = HorizontalAlignment.Center;
            probe.RunSetChildPosition(child, 12.5f);
            Near(target.anchoredPosition, new Vector2(0f, -12.5f));
            probe.ChildAlignment = HorizontalAlignment.Right;
            probe.RunSetChildPosition(child, 12.5f);
            Near(target.anchoredPosition, new Vector2(-2f, -12.5f));
            probe.ChildAlignment = (HorizontalAlignment)3;
            probe.RunSetChildPosition(child, 99f);
            Near(target.anchoredPosition, new Vector2(-2f, -12.5f));
            Near(((RectTransform)child.transform).anchoredPosition, new Vector2(9f, 9f));
        }
    }
}
