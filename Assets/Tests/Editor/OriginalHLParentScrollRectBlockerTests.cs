using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;
using Recorder = ProjectLucid.Tests.OriginalPotentialDragRecorderProbe;

namespace ProjectLucid.Tests
{
    // Behaviour of HLParentScrollRectBlocker (HLUnityUI.Runtime 0x0600006e..75) against the
    // shipped x86-64 bodies. Every case owns an inactive HideAndDontSave hierarchy and
    // destroys it in TearDown; the event data are standalone PointerEventData objects with
    // no EventSystem. The recorders are test-only IInitializePotentialDragHandler components.
    [TestFixture]
    public sealed class OriginalHLParentScrollRectBlockerTests
    {
        const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        readonly List<GameObject> roots = new List<GameObject>();

        [TearDown]
        public void DestroyOwnedRoots()
        {
            foreach (GameObject root in roots)
                if (root != null) Object.DestroyImmediate(root);
            roots.Clear();
        }

        GameObject NewObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
            if (parent == null)
            {
                roots.Add(gameObject);
                gameObject.SetActive(false);
            }
            else
            {
                gameObject.transform.SetParent(parent, false);
            }
            return gameObject;
        }

        static PointerEventData Pointer(int pointerId, PointerEventData.InputButton button = PointerEventData.InputButton.Left) =>
            new PointerEventData(null) { pointerId = pointerId, button = button };

        static int TrackedPointerId(HLParentScrollRectBlocker blocker) =>
            (int)typeof(HLParentScrollRectBlocker).GetField("m_trackedPointerId", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(blocker);

        static bool TrackingPointer(HLParentScrollRectBlocker blocker) =>
            (bool)typeof(HLParentScrollRectBlocker).GetField("m_trackingPointer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(blocker);

        // dump.cs TypeDefIndex 11837/11836 and DummyDll metadata: global namespace,
        // attribute and interface order, the two private fields, public virtual new-slot
        // handlers, the IEventSystemHandler constraint and the display class name.
        [Test]
        public void DeclarationKeepsTheDumpedInterfacesFieldsAndDisplayClass()
        {
            Type type = typeof(HLParentScrollRectBlocker);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.FullName, Is.EqualTo("HLParentScrollRectBlocker"));
            Assert.That(type.BaseType, Is.EqualTo(typeof(UIBehaviour)));
            Assert.That(type.IsAbstract || type.IsSealed, Is.False);
            CustomAttributeData[] attributes = type.GetCustomAttributesData().ToArray();
            Assert.That(attributes.Select(a => a.AttributeType).Distinct().Single(), Is.EqualTo(typeof(Il2CppSetOptionAttribute)));
            Assert.That(attributes.Select(a => Convert.ToInt32(a.ConstructorArguments[0].Value)).ToArray(), Is.EqualTo(new[] { 2, 1 }));
            CollectionAssert.IsSubsetOf(new[] { typeof(IInitializePotentialDragHandler), typeof(IEventSystemHandler),
                typeof(IBeginDragHandler), typeof(IEndDragHandler), typeof(IDragHandler) }, type.GetInterfaces());

            FieldInfo[] fields = type.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "m_trackedPointerId", "m_trackingPointer" }));
            Assert.That(fields.Select(f => f.FieldType).ToArray(), Is.EqualTo(new[] { typeof(int), typeof(bool) }));
            Assert.That(fields.All(f => f.IsPrivate && f.GetCustomAttributesData().Count == 0), Is.True);

            foreach (string name in new[] { "OnInitializePotentialDrag", "OnBeginDrag", "OnEndDrag", "OnDrag" })
            {
                MethodInfo method = type.GetMethod(name, Declared);
                Assert.That(method.IsPublic && method.IsVirtual && !method.IsFinal && (method.Attributes & MethodAttributes.NewSlot) != 0, Is.True, name);
            }
            MethodInfo forward = type.GetMethod("DoForFirstFoundParent", Declared);
            Assert.That(forward.IsPrivate && forward.IsGenericMethodDefinition, Is.True);
            Assert.That(forward.GetGenericArguments().Single().GetGenericParameterConstraints(), Is.EqualTo(new[] { typeof(IEventSystemHandler) }));
            Assert.That(type.GetNestedTypes(Declared).Select(t => t.Name).ToArray(), Is.EqualTo(new[] { "<>c__DisplayClass2_0" }));
        }

        // .ctor 0x1B3B9F0 stores -1; OnBeginDrag 0x1B3B9A0 tracks only the first pointer.
        // OnEndDrag 0x1B3B9C0 releases only for that pointer and keeps its id; OnDrag
        // 0x1B3B9E0 is empty.
        [Test]
        public void PointerTrackingFollowsTheFirstPointerUntilItsDragEnds()
        {
            HLParentScrollRectBlocker blocker = NewObject("Blocker", null).AddComponent<HLParentScrollRectBlocker>();
            Assert.That(TrackedPointerId(blocker), Is.EqualTo(-1));
            Assert.That(TrackingPointer(blocker), Is.False);

            blocker.OnBeginDrag(Pointer(5));
            blocker.OnBeginDrag(Pointer(7));
            Assert.That(TrackedPointerId(blocker), Is.EqualTo(5));
            Assert.That(TrackingPointer(blocker), Is.True);

            blocker.OnDrag(Pointer(5));
            blocker.OnEndDrag(Pointer(7));
            Assert.That(TrackingPointer(blocker), Is.True);
            blocker.OnEndDrag(Pointer(5));
            Assert.That(TrackingPointer(blocker), Is.False);
            Assert.That(TrackedPointerId(blocker), Is.EqualTo(5));

            blocker.OnBeginDrag(Pointer(7));
            Assert.That(TrackedPointerId(blocker), Is.EqualTo(7));
            Assert.That(TrackingPointer(blocker), Is.True);
        }

        // 0x1B3B930: only InputButton.Left (0) is forwarded; DoForFirstFoundParent starts at
        // the parent, stops at the first handler component and passes the same event data.
        [Test]
        public void PotentialDragReachesOnlyTheFirstParentHandlerForTheLeftButton()
        {
            GameObject grandparent = NewObject("Grandparent", null);
            Recorder distant = grandparent.AddComponent<Recorder>();
            GameObject parent = NewObject("Parent", grandparent.transform);
            Recorder first = parent.AddComponent<Recorder>();
            Recorder second = parent.AddComponent<Recorder>();
            GameObject self = NewObject("Blocker", parent.transform);
            Recorder own = self.AddComponent<Recorder>();
            HLParentScrollRectBlocker blocker = self.AddComponent<HLParentScrollRectBlocker>();

            PointerEventData left = Pointer(1);
            blocker.OnInitializePotentialDrag(left);
            Assert.That(first.Received.Single(), Is.SameAs(left));
            Assert.That(second.Received, Is.Empty);
            Assert.That(distant.Received, Is.Empty);
            Assert.That(own.Received, Is.Empty);

            blocker.OnInitializePotentialDrag(Pointer(1, PointerEventData.InputButton.Right));
            blocker.OnInitializePotentialDrag(Pointer(1, PointerEventData.InputButton.Middle));
            Assert.That(first.Received.Count, Is.EqualTo(1));
            Assert.That(TrackingPointer(blocker), Is.False, "the potential drag does not track the pointer");
        }

        // 0x92FBC0: ancestors without a handler are skipped and the search continues up the
        // hierarchy; with no handler anywhere nothing is invoked.
        [Test]
        public void PotentialDragSkipsAncestorsWithoutAHandler()
        {
            GameObject grandparent = NewObject("Grandparent", null);
            Recorder distant = grandparent.AddComponent<Recorder>();
            GameObject parent = NewObject("Parent", grandparent.transform);
            HLParentScrollRectBlocker blocker = NewObject("Blocker", parent.transform).AddComponent<HLParentScrollRectBlocker>();
            PointerEventData left = Pointer(2);
            blocker.OnInitializePotentialDrag(left);
            Assert.That(distant.Received.Single(), Is.SameAs(left));

            HLParentScrollRectBlocker lone = NewObject("Lone", null).AddComponent<HLParentScrollRectBlocker>();
            Assert.DoesNotThrow(() => lone.OnInitializePotentialDrag(Pointer(2)));
        }
    }
}
