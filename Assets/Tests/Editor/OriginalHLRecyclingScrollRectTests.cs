using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using Element = ProjectLucid.Tests.OriginalRecyclingScrollRectElementProbe;
using Object = UnityEngine.Object;
using Probe = ProjectLucid.Tests.OriginalRecyclingScrollRectProbe;

namespace ProjectLucid.Tests
{
    // Behaviour of the abstract Hardlight.HLRecyclingScrollRect`2 (HLUnityUI.Runtime
    // 0x06000653..67e) and HLRecyclingScrollRectElement`1 (0x0600067f..683) against the
    // fully shared x86-64 bodies, driven through test-only subclasses (no original
    // subclass exists). The probe's five sizes are fixture values, so the base arithmetic
    // is checked against hand-computed numbers; the probe records the slot 24 child
    // positions and the slot 26 destroys. Every case owns an inactive HideAndDontSave root
    // (so no engine callback runs and SetupAtEndOfFrame never starts a coroutine) and
    // destroys it in TearDown; the HLScrollRect used by SetEntries, RefreshView and the
    // listener cases stays disabled on that root. Expected values come from the native
    // bodies cited per case.
    [TestFixture]
    public sealed class OriginalHLRecyclingScrollRectTests
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

        GameObject NewRoot()
        {
            var root = new GameObject("ProjectLucid.HLRecyclingScrollRect.Owned", typeof(RectTransform));
            roots.Add(root);
            root.hideFlags = HideFlags.HideAndDontSave;
            root.SetActive(false);
            return root;
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

        // Start 10, end 6, element 40, spacing 5, viewport 100, allowance 20 (the default).
        Probe Build()
        {
            GameObject root = NewRoot();
            Probe probe = root.AddComponent<Probe>();
            probe.Content = NewRect("Content", root.transform, new Vector2(100f, 100f));
            RectTransform prefab = NewRect("Row", root.transform, new Vector2(40f, 40f));
            probe.Prefab = prefab.gameObject.AddComponent<Element>();
            probe.StartPaddingValue = 10f;
            probe.EndPaddingValue = 6f;
            probe.ElementSizeValue = 40f;
            probe.Spacing = 5f;
            probe.ViewportSizeValue = 100f;
            return probe;
        }

        static HLScrollRect AttachScroll(Probe probe)
        {
            HLScrollRect scroll = probe.gameObject.AddComponent<HLScrollRect>();
            scroll.enabled = false;
            RectTransform viewport = NewRect("Viewport", probe.transform, new Vector2(100f, 100f));
            scroll.viewport = viewport;
            scroll.content = NewRect("ScrollContent", viewport, new Vector2(100f, 300f));
            probe.Scroll = scroll;
            return scroll;
        }

        // A ring of existing objects O0..On-1 owned by the probe.
        static Element[] Ring(Probe probe, int count, int indexOfFirstObject, int indexOfFirstEntry)
        {
            var objects = new Element[count];
            for (int i = 0; i < count; i++)
            {
                objects[i] = NewRect("O" + i, probe.Content, new Vector2(40f, 40f)).gameObject.AddComponent<Element>();
                probe.Objects.Add(objects[i]);
            }
            probe.ObjectCount = count;
            probe.IndexOfFirstObject = indexOfFirstObject;
            probe.IndexOfFirstEntryField = indexOfFirstEntry;
            return objects;
        }

        // Data i * 10 for i < count; m_dataCount is set separately where a case needs it.
        static void Data(Probe probe, int count, int dataCount)
        {
            probe.Data = Enumerable.Range(0, count).Select(i => i * 10).ToList();
            probe.DataCount = dataCount;
        }

        static string[] Names(IEnumerable<Element> elements) => elements.Select(e => e.name).ToArray();
        static float[] Positions(Probe probe) => probe.ChildPositions.Select(p => p.Position).ToArray();
        static string[] PositionNames(Probe probe) => probe.ChildPositions.Select(p => p.Name).ToArray();

        static void AssertReceived(Element element, params int[] expected) =>
            Assert.That(element.Received.ToArray(), Is.EqualTo(expected), element.name);

        // dump.cs TypeDefIndex 12057/12056 and DummyDll metadata: attributes, the
        // T1 : HLRecyclingScrollRectElement<T2> constraint, field order, access, types and
        // attribute order, the abstract members and the compiler-generated names that the
        // member order fixes.
        [Test]
        public void DeclarationKeepsTheDumpedConstraintFieldsAndGeneratedNames()
        {
            Type type = typeof(HLRecyclingScrollRect<,>);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.FullName, Is.EqualTo("Hardlight.HLRecyclingScrollRect`2"));
            Assert.That(type.IsAbstract && type.IsPublic, Is.True);
            Assert.That(type.BaseType, Is.EqualTo(typeof(UIBehaviour)));
            CustomAttributeData[] attributes = type.GetCustomAttributesData().ToArray();
            Assert.That(attributes.Select(a => a.AttributeType).ToArray(),
                Is.EqualTo(new[] { typeof(Il2CppSetOptionAttribute), typeof(Il2CppSetOptionAttribute) }));
            Assert.That(attributes.Select(a => Convert.ToInt32(a.ConstructorArguments[0].Value)).ToArray(), Is.EqualTo(new[] { 2, 1 }));
            Assert.That(attributes.All(a => Equals(a.ConstructorArguments[1].Value, false)), Is.True);

            Type[] parameters = type.GetGenericArguments();
            Assert.That(parameters.Select(p => p.Name).ToArray(), Is.EqualTo(new[] { "T1", "T2" }));
            Assert.That(parameters[0].GetGenericParameterConstraints(),
                Is.EqualTo(new[] { typeof(HLRecyclingScrollRectElement<>).MakeGenericType(parameters[1]) }));
            Assert.That(parameters[1].GetGenericParameterConstraints(), Is.Empty);

            FieldInfo[] fields = type.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[]
            {
                "m_listEntryPrefab", "m_scrollRect", "m_content", "m_externalPadding", "m_internalPadding", "m_spacing",
                "m_regenerateOnSizeChanged", "m_allowance", "m_objects", "m_data", "m_indexOfFirstEntry",
                "m_indexOfFirstObject", "m_objectCount", "m_dataCount", "m_resizeOffset", "m_maxObjectsRequired",
                "m_waitForEndOfFrame", "m_isDirty",
            }));
            Assert.That(fields.Select(f => f.FieldType).ToArray(), Is.EqualTo(new[]
            {
                parameters[0], typeof(HLScrollRect), typeof(RectTransform), typeof(Vector2), typeof(RectOffset), typeof(float),
                typeof(bool), typeof(float), typeof(List<>).MakeGenericType(parameters[0]),
                typeof(IReadOnlyList<>).MakeGenericType(parameters[1]), typeof(int), typeof(int), typeof(int), typeof(int),
                typeof(int), typeof(int), typeof(WaitForEndOfFrame), typeof(bool),
            }));
            Assert.That(fields.All(f => f.IsFamily && !f.IsStatic), Is.True);
            Assert.That(fields.Where(f => f.IsInitOnly).Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "m_objects" }));
            // Dumped attribute order per serialized field; null means no tooltip.
            var serialized = new (string Name, bool TooltipFirst, string Tooltip)[]
            {
                ("m_listEntryPrefab", false, null), ("m_scrollRect", false, null),
                ("m_content", false, "List Elements will be added inside this object"),
                ("m_externalPadding", false, "Total padding at the beginning and end of the Content Object, within the Scroll Rect Content object"),
                ("m_internalPadding", true, "Padding within the Content object"), ("m_spacing", false, null),
                ("m_regenerateOnSizeChanged", true, "When this option is selected, if the scroll rect is resized, new children will be added/removed as required"),
                ("m_allowance", false, null),
            };
            for (int i = 0; i < fields.Length; i++)
            {
                Type[] actual = fields[i].GetCustomAttributesData().Select(a => a.AttributeType).ToArray();
                if (i >= serialized.Length)
                {
                    Assert.That(actual, Is.Empty, fields[i].Name);
                    continue;
                }
                Type[] expected = serialized[i].Tooltip == null ? new[] { typeof(SerializeField) }
                    : serialized[i].TooltipFirst ? new[] { typeof(TooltipAttribute), typeof(SerializeField) }
                    : new[] { typeof(SerializeField), typeof(TooltipAttribute) };
                Assert.That(actual, Is.EqualTo(expected), fields[i].Name);
                if (serialized[i].Tooltip != null)
                    Assert.That(fields[i].GetCustomAttribute<TooltipAttribute>().tooltip, Is.EqualTo(serialized[i].Tooltip));
            }

            Assert.That(type.GetProperties(Declared).Select(p => p.Name).ToArray(),
                Is.EqualTo(new[] { "StartPadding", "EndPadding", "ContentSize", "ViewportSize", "ElementSize" }));
            Assert.That(type.GetProperty("ContentSize", Declared).SetMethod.IsAbstract, Is.True);
            foreach (string name in new[] { "get_StartPadding", "get_EndPadding", "get_ContentSize", "set_ContentSize",
                "get_ViewportSize", "get_ElementSize", "GetNormalizedPositionOnAxis", "SetChildPosition" })
            {
                MethodInfo method = type.GetMethod(name, Declared);
                Assert.That(method.IsAbstract && method.IsFamily, Is.True, name);
            }
            foreach (string name in new[] { "Instantiate", "Destroy", "ContentReady" })
            {
                MethodInfo method = type.GetMethod(name, Declared);
                Assert.That(method.IsVirtual && !method.IsAbstract && method.IsFamily && (method.Attributes & MethodAttributes.NewSlot) != 0, Is.True, name);
            }
            foreach (string name in new[] { "OnEnable", "OnDisable", "OnRectTransformDimensionsChange" })
            {
                MethodInfo method = type.GetMethod(name, Declared);
                Assert.That(method.IsFamily && method.GetBaseDefinition().DeclaringType == typeof(UIBehaviour), Is.True, name);
            }
            MethodInfo setEntries = type.GetMethod("SetEntries", Declared);
            Assert.That(setEntries.IsPublic && !setEntries.IsVirtual, Is.True);
            Assert.That(setEntries.GetParameters()[1].DefaultValue, Is.EqualTo(true));
            foreach (string name in new[] { "IncrementLoopingIndex", "DecrementLoopingIndex", "Mod" })
                Assert.That(type.GetMethod(name, Declared).IsStatic, Is.True, name);
            Assert.That(type.GetMethod("<OnEnable>b__35_0", Declared).IsPrivate, Is.True);
            Assert.That(type.GetMethod("<OnDisable>b__36_0", Declared).IsPrivate, Is.True);
            Assert.That(type.GetNestedTypes(Declared).Select(t => t.Name).ToArray(), Is.EqualTo(new[] { "<Coroutine_SetupAtEndOfFrame>d__53" }));
            Assert.That(type.GetConstructors(Declared).Single().IsFamily, Is.True);
        }

        // dump.cs TypeDefIndex 12058; the static constructor 0x06000683 stores typeof(T)
        // per closed type.
        [Test]
        public void ElementDeclarationAndDataTypeIsTheClosedTypeArgument()
        {
            Type type = typeof(HLRecyclingScrollRectElement<>);
            Assert.That(type.FullName, Is.EqualTo("Hardlight.HLRecyclingScrollRectElement`1"));
            Assert.That(type.IsAbstract && type.BaseType == typeof(MonoBehaviour), Is.True);
            Assert.That(type.GetCustomAttributesData().Select(a => Convert.ToInt32(a.ConstructorArguments[0].Value)).ToArray(), Is.EqualTo(new[] { 2, 1 }));
            FieldInfo[] fields = type.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "m_rectTransform", "DataType" }));
            Assert.That(fields[0].IsPrivate && fields[0].IsDefined(typeof(SerializeField)), Is.True);
            Assert.That(fields[1].IsPublic && fields[1].IsStatic && fields[1].FieldType == typeof(Type), Is.True);
            Assert.That(type.GetMethod("Initialise", Declared).IsAbstract, Is.True);
            MethodInfo onScroll = type.GetMethod("OnScroll", Declared);
            Assert.That(onScroll.IsPublic && onScroll.IsVirtual && !onScroll.IsAbstract, Is.True);

            Assert.That(HLRecyclingScrollRectElement<int>.DataType, Is.EqualTo(typeof(int)));
            Assert.That(HLRecyclingScrollRectElement<string>.DataType, Is.EqualTo(typeof(string)));

            GameObject root = NewRoot();
            Element element = root.AddComponent<Element>();
            Assert.That(element.RectTransform == null, Is.True);
            RectTransform other = NewRect("Other", root.transform, Vector2.one);
            // Late-bound writes need the closed type's FieldInfo, not the open definition's.
            typeof(HLRecyclingScrollRectElement<int>).GetField("m_rectTransform", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(element, other);
            Assert.That(element.RectTransform, Is.SameAs(other));
        }

        // .ctor 0x1394860: zero external padding, a fresh RectOffset, allowance 20, empty
        // object and data lists, first object -1, resize offset 1 and a WaitForEndOfFrame.
        [Test]
        public void ConstructorSeedsTheRingStateAndDefaults()
        {
            Probe probe = Build();
            Probe other = Build();
            Assert.That(probe.ExternalPadding, Is.EqualTo(Vector2.zero));
            Assert.That(probe.InternalPadding, Is.Not.Null.And.Not.SameAs(other.InternalPadding));
            Assert.That(new[] { probe.InternalPadding.left, probe.InternalPadding.right, probe.InternalPadding.top, probe.InternalPadding.bottom },
                Is.EqualTo(new[] { 0, 0, 0, 0 }));
            Assert.That(probe.Allowance, Is.EqualTo(20f));
            Assert.That(probe.Objects, Is.Empty);
            Assert.That(probe.Data, Is.InstanceOf<List<int>>().And.Empty);
            Assert.That(probe.IndexOfFirstEntryField, Is.Zero);
            Assert.That(probe.IndexOfFirstObject, Is.EqualTo(-1));
            Assert.That(probe.ObjectCount, Is.Zero);
            Assert.That(probe.DataCount, Is.Zero);
            Assert.That(probe.ResizeOffset, Is.EqualTo(1));
            Assert.That(probe.MaxObjectsRequired, Is.Zero);
            Assert.That(probe.WaitForEndOfFrameField, Is.Not.Null.And.Not.SameAs(other.WaitForEndOfFrameField));
            Assert.That(probe.IsDirty, Is.False);
            Assert.That(probe.RegenerateOnSizeChanged, Is.False);
        }

        // 0x06000673..675: increment wraps with the C# remainder, decrement only wraps 0,
        // and Mod returns a non-negative remainder.
        [Test]
        public void LoopingIndexHelpersKeepTheirOwnWrapRules()
        {
            Assert.That(Probe.RunIncrementLoopingIndex(3, 4), Is.Zero);
            Assert.That(Probe.RunIncrementLoopingIndex(1, 4), Is.EqualTo(2));
            Assert.That(Probe.RunIncrementLoopingIndex(-2, 4), Is.EqualTo(-1));
            Assert.That(Probe.RunDecrementLoopingIndex(0, 4), Is.EqualTo(3));
            Assert.That(Probe.RunDecrementLoopingIndex(2, 4), Is.EqualTo(1));
            Assert.That(Probe.RunDecrementLoopingIndex(5, 4), Is.EqualTo(4));
            Assert.That(Probe.RunDecrementLoopingIndex(-1, 4), Is.EqualTo(-2));
            Assert.That(Probe.RunMod(-1, 4), Is.EqualTo(3));
            Assert.That(Probe.RunMod(-4, 4), Is.Zero);
            Assert.That(Probe.RunMod(-5, 4), Is.EqualTo(3));
            Assert.That(Probe.RunMod(5, 4), Is.EqualTo(1));
        }

        // 0x06000667: content = start + end + element * n + spacing * max(0, n - 1) with
        // n = m_dataCount + m_resizeOffset.
        [Test]
        public void AdjustSizeCountsTheResizeOffsetAndNeverNegativeSpacing()
        {
            Probe probe = Build();
            probe.DataCount = 4;
            probe.RunAdjustSizeToFitContents();
            probe.DataCount = 0;
            probe.RunAdjustSizeToFitContents();
            probe.ResizeOffset = 0;
            probe.RunAdjustSizeToFitContents();
            Assert.That(probe.ContentSizeWrites.ToArray(), Is.EqualTo(new[] { 236f, 56f, 16f }));
        }

        // 0x0600066f: start padding plus one element-and-spacing step per entry.
        [Test]
        public void PositionFromEntryIndexStepsByElementAndSpacing()
        {
            Probe probe = Build();
            Assert.That(probe.RunPositionFromEntryIndex(0), Is.EqualTo(10f));
            Assert.That(probe.RunPositionFromEntryIndex(3), Is.EqualTo(145f));
            Assert.That(probe.RunPositionFromEntryIndex(-1), Is.EqualTo(-35f));
        }

        // 0x0600066a (roundss 0xa at 0x13941bf is a ceiling): the scrolled distance less
        // start, leading external padding (x), allowance and one element, over element +
        // spacing, clamped to [0, m_dataCount - 1].
        [Test]
        public void IndexOfFirstEntryCeilsTheScrolledStepsAndClamps()
        {
            Probe probe = Build();
            probe.ContentSizeValue = 500f;
            probe.ExternalPadding = new Vector2(3f, 50f);
            probe.DataCount = 10;
            Assert.That(probe.RunIndexOfFirstEntry(1f), Is.Zero);
            Assert.That(probe.RunIndexOfFirstEntry(0.5f), Is.EqualTo(3));
            Assert.That(probe.RunIndexOfFirstEntry(0f), Is.EqualTo(8));
            Assert.That(probe.RunIndexOfFirstEntry(1.5f), Is.Zero);
            probe.DataCount = 5;
            Assert.That(probe.RunIndexOfFirstEntry(0f), Is.EqualTo(4));

            // 163 scrolled - 73 = exactly two steps; using the trailing padding (50) would
            // give one.
            probe.ContentSizeValue = 263f;
            Assert.That(probe.RunIndexOfFirstEntry(0f), Is.EqualTo(2));
            // Content shorter than the viewport scrolls nothing.
            probe.ContentSizeValue = 50f;
            Assert.That(probe.RunIndexOfFirstEntry(0f), Is.Zero);
        }

        // 0x06000664 (roundss 0x9 at 0x13939c7 is a floor): (100 + 2 * 20) / 45 = 3.1 gives
        // five objects, all added at the end for entries 0..4.
        [Test]
        public void SetupContentFloorsTheVisibleCountAndAddsAtTheEnd()
        {
            Probe probe = Build();
            Data(probe, 7, 7);
            probe.RunSetupContent();
            Assert.That(probe.MaxObjectsRequired, Is.EqualTo(5));
            Assert.That(Names(probe.Objects), Is.EqualTo(new[] { "Row0", "Row1", "Row2", "Row3", "Row4" }));
            Assert.That(probe.Objects.Select(o => o.Received.Single()).ToArray(), Is.EqualTo(new[] { 0, 10, 20, 30, 40 }));
            Assert.That(Positions(probe), Is.EqualTo(new[] { 10f, 55f, 100f, 145f, 190f }));
            Assert.That(probe.IndexOfFirstObject, Is.Zero);
            Assert.That(probe.ObjectCount, Is.EqualTo(5));
            Assert.That(probe.Objects.All(o => o.transform.parent == probe.Content), Is.True);
            Assert.That(probe.Calls.Last(), Is.EqualTo("ContentReady"));
            Assert.That(probe.Calls.Count(c => c == "ContentReady"), Is.EqualTo(1));
        }

        // 0x1393a05..0x1393a3d: the end share is min(delta, m_dataCount - first + 1), so it
        // reaches one entry past the count; the rest go to the start slot one by one while
        // m_indexOfFirstEntry stays 5.
        [Test]
        public void SetupContentSplitsWithAnOffByOneEndShare()
        {
            Probe probe = Build();
            Data(probe, 7, 6);
            probe.IndexOfFirstEntryField = 5;
            probe.RunSetupContent();
            Assert.That(PositionNames(probe), Is.EqualTo(new[] { "Row0", "Row1", "Row0", "Row0", "Row0" }));
            Assert.That(Positions(probe), Is.EqualTo(new[] { 235f, 280f, 190f, 145f, 100f }));
            Assert.That(probe.Objects.Select(o => o.Received.Single()).ToArray(), Is.EqualTo(new[] { 20, 30, 40, 50, 60 }));
            Assert.That(probe.IndexOfFirstEntryField, Is.EqualTo(5));
            Assert.That(probe.IndexOfFirstObject, Is.Zero);
            Assert.That(probe.ObjectCount, Is.EqualTo(5));
        }

        // 0x1393a25: a smaller visible count removes the surplus before ContentReady.
        [Test]
        public void SetupContentRemovesSurplusObjects()
        {
            Probe probe = Build();
            Element[] ring = Ring(probe, 5, 2, 0);
            Data(probe, 2, 2);
            probe.RunSetupContent();
            Assert.That(probe.Destroyed.ToArray(), Is.EqualTo(new[] { ring[0], ring[1], ring[4] }));
            Assert.That(probe.Objects.ToArray(), Is.EqualTo(new[] { ring[2], ring[3] }));
            Assert.That(probe.Calls.ToArray(), Is.EqualTo(new[] { "Destroy", "Destroy", "Destroy", "ContentReady" }));
        }

        // 0x0600066e: the slots just before the first object (the end of the ring) go
        // first, then the end of the list; the end range is removed before the first.
        [Test]
        public void RemoveObjectsTakesTheRingEndBeforeTheFirstObject()
        {
            Probe probe = Build();
            Element[] ring = Ring(probe, 5, 4, 0);
            probe.RunRemoveObjects(2);
            Assert.That(probe.Destroyed.ToArray(), Is.EqualTo(new[] { ring[2], ring[3] }));
            Assert.That(probe.Objects.ToArray(), Is.EqualTo(new[] { ring[0], ring[1], ring[4] }));
            Assert.That(probe.IndexOfFirstObject, Is.EqualTo(2));
            Assert.That(probe.ObjectCount, Is.EqualTo(3));
        }

        // 0x1394655/0x1394677: with the constructor's -1 the start share is -1, so after the
        // end range is destroyed and removed, RemoveRange(0, -1) throws and the first-object
        // index is left at -1.
        [Test]
        public void RemoveObjectsWithNegativeFirstObjectThrowsAfterTrimmingTheEnd()
        {
            Probe probe = Build();
            Element[] ring = Ring(probe, 3, -1, 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => probe.RunRemoveObjects(1));
            Assert.That(probe.Destroyed.ToArray(), Is.EqualTo(new[] { ring[1], ring[2] }));
            Assert.That(probe.Objects.ToArray(), Is.EqualTo(new[] { ring[0] }));
            Assert.That(probe.IndexOfFirstObject, Is.EqualTo(-1));
            Assert.That(probe.ObjectCount, Is.EqualTo(3));
        }

        // 0x0600066b: new objects are inserted from the first-object slot for the entries
        // after the last one, and the first-object index moves past them.
        [Test]
        public void AddNewObjectsToEndInsertsAtTheFirstObjectSlot()
        {
            Probe probe = Build();
            Element[] ring = Ring(probe, 3, 1, 4);
            Data(probe, 10, 10);
            probe.RunAddNewObjectsToEnd(2);
            Assert.That(Names(probe.Objects), Is.EqualTo(new[] { "O0", "Row1", "Row2", "O1", "O2" }));
            Assert.That(probe.Objects[1].Received.ToArray(), Is.EqualTo(new[] { 70 }));
            Assert.That(probe.Objects[2].Received.ToArray(), Is.EqualTo(new[] { 80 }));
            Assert.That(Positions(probe), Is.EqualTo(new[] { 325f, 370f }));
            Assert.That(probe.IndexOfFirstObject, Is.EqualTo(3));
            Assert.That(probe.Objects[probe.IndexOfFirstObject], Is.SameAs(ring[1]));
            Assert.That(probe.ObjectCount, Is.EqualTo(5));
        }

        // 0x13942a8..0x13942b7: a negative first-object index becomes 0 rather than
        // first + amount, even when nothing is added.
        [Test]
        public void AddNewObjectsToEndRestartsANegativeFirstObjectAtZero()
        {
            Probe probe = Build();
            Data(probe, 3, 3);
            probe.RunAddNewObjectsToEnd(0);
            Assert.That(probe.IndexOfFirstObject, Is.Zero);
            Assert.That(probe.Objects, Is.Empty);

            probe.IndexOfFirstObject = -1;
            probe.RunAddNewObjectsToEnd(2);
            Assert.That(Names(probe.Objects), Is.EqualTo(new[] { "Row0", "Row1" }));
            Assert.That(probe.IndexOfFirstObject, Is.Zero);
            Assert.That(probe.ObjectCount, Is.EqualTo(2));
        }

        // 0x0600066d: Replace swaps every "(Clone)" for the object index, then the object is
        // inserted, initialised with its entry and positioned.
        [Test]
        public void InstantiateNewObjectRenamesInsertsInitialisesAndPositions()
        {
            Probe probe = Build();
            probe.Prefab.name = "Cell(Clone)";
            Element[] ring = Ring(probe, 1, 0, 0);
            Data(probe, 5, 5);
            probe.RunInstantiateNewObject(1, 3);
            Element created = probe.Objects[1];
            Assert.That(created.name, Is.EqualTo("Cell11"));
            Assert.That(probe.Prefab.name, Is.EqualTo("Cell(Clone)"));
            Assert.That(probe.Objects.ToArray(), Is.EqualTo(new[] { ring[0], created }));
            Assert.That(created.Received.ToArray(), Is.EqualTo(new[] { 30 }));
            Assert.That(probe.ChildPositions.Single().Child, Is.SameAs(created));
            Assert.That(Positions(probe), Is.EqualTo(new[] { 145f }));
            Assert.That(created.transform.parent, Is.SameAs(probe.Content));
            Assert.That(probe.ObjectCount, Is.EqualTo(1), "InstantiateNewObject leaves the count to its callers");
        }

        // 0x06000662/0x06000663: Instantiate clones the prefab under m_content; Destroy goes
        // through Object.Destroy on the GameObject, which edit mode rejects with an error.
        [Test]
        public void BaseInstantiateParentsTheCloneAndBaseDestroyUsesObjectDestroy()
        {
            Probe probe = Build();
            Element clone = probe.RunInstantiate();
            Assert.That(clone, Is.Not.SameAs(probe.Prefab));
            Assert.That(clone.name, Is.EqualTo("Row(Clone)"));
            Assert.That(clone.transform.parent, Is.SameAs(probe.Content));

            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("^Destroy may not be called from edit mode"));
            probe.RunBaseDestroy(clone);
            Assert.That(clone != null, Is.True);
        }

        // 0x06000669, delta < count: the first two objects take entries 6 and 7 and the
        // indices advance by the delta.
        [Test]
        public void MoveFromStartToEndReassignsTheLeadingObjects()
        {
            Probe probe = Build();
            Element[] ring = Ring(probe, 4, 1, 2);
            Data(probe, 20, 20);
            probe.RunMoveFromStartToEnd(2);
            Assert.That(PositionNames(probe), Is.EqualTo(new[] { "O1", "O2" }));
            Assert.That(Positions(probe), Is.EqualTo(new[] { 280f, 325f }));
            AssertReceived(ring[1], 60);
            AssertReceived(ring[2], 70);
            AssertReceived(ring[3]);
            Assert.That(probe.IndexOfFirstEntryField, Is.EqualTo(4));
            Assert.That(probe.IndexOfFirstObject, Is.EqualTo(3));
        }

        // 0x1393ef2: a delta of at least the object count jumps straight to entry first +
        // delta and to object (first + delta) % count.
        [Test]
        public void MoveFromStartToEndJumpsPastAWholeRing()
        {
            Probe probe = Build();
            Element[] ring = Ring(probe, 4, 1, 2);
            Data(probe, 20, 20);
            probe.RunMoveFromStartToEnd(6);
            Assert.That(PositionNames(probe), Is.EqualTo(new[] { "O3", "O0", "O1", "O2" }));
            Assert.That(Positions(probe), Is.EqualTo(new[] { 370f, 415f, 460f, 505f }));
            AssertReceived(ring[3], 80);
            AssertReceived(ring[0], 90);
            Assert.That(probe.IndexOfFirstEntryField, Is.EqualTo(8));
            Assert.That(probe.IndexOfFirstObject, Is.EqualTo(3));
        }

        // 0x1393ed0 returns before the index updates, while MoveFromEndToStart (0x1393c96)
        // always rewrites its indices, normalising the first object through Mod.
        [Test]
        public void NonPositiveMovesOnlyRewriteTheIndicesWhenMovingToTheStart()
        {
            Probe probe = Build();
            Ring(probe, 4, 5, 2);
            Data(probe, 20, 20);
            probe.RunMoveFromStartToEnd(0);
            probe.RunMoveFromStartToEnd(-1);
            Assert.That(probe.IndexOfFirstEntryField, Is.EqualTo(2));
            Assert.That(probe.IndexOfFirstObject, Is.EqualTo(5));
            probe.RunMoveFromEndToStart(0);
            Assert.That(probe.IndexOfFirstEntryField, Is.EqualTo(2));
            Assert.That(probe.IndexOfFirstObject, Is.EqualTo(1));
            Assert.That(probe.ChildPositions, Is.Empty);
            Assert.That(probe.Objects.All(o => o.Received.Count == 0), Is.True);
        }

        // 0x06000668: the indices move back by the delta first, then the new first objects
        // take the earlier entries.
        [Test]
        public void MoveFromEndToStartReassignsFromTheNewFirstObject()
        {
            Probe probe = Build();
            Element[] ring = Ring(probe, 4, 1, 6);
            Data(probe, 20, 20);
            probe.RunMoveFromEndToStart(2);
            Assert.That(PositionNames(probe), Is.EqualTo(new[] { "O3", "O0" }));
            Assert.That(Positions(probe), Is.EqualTo(new[] { 190f, 235f }));
            AssertReceived(ring[3], 40);
            AssertReceived(ring[0], 50);
            Assert.That(probe.IndexOfFirstEntryField, Is.EqualTo(4));
            Assert.That(probe.IndexOfFirstObject, Is.EqualTo(3));

            // A delta past the whole ring reassigns every object from Mod(1 - 6, 4) = 3.
            probe.ChildPositions.Clear();
            probe.IndexOfFirstEntryField = 6;
            probe.IndexOfFirstObject = 1;
            probe.RunMoveFromEndToStart(6);
            Assert.That(PositionNames(probe), Is.EqualTo(new[] { "O3", "O0", "O1", "O2" }));
            Assert.That(Positions(probe), Is.EqualTo(new[] { 10f, 55f, 100f, 145f }));
            AssertReceived(ring[1], 20);
            AssertReceived(ring[2], 30);
            Assert.That(probe.IndexOfFirstEntryField, Is.Zero);
            Assert.That(probe.IndexOfFirstObject, Is.EqualTo(3));
        }

        // 0x06000666: no data means no axis query; otherwise the target index is compared
        // with the first entry and every object gets OnScroll afterwards.
        [Test]
        public void PositionChangedMovesTowardsTheTargetEntryAndNotifiesEveryObject()
        {
            Probe probe = Build();
            Element[] ring = Ring(probe, 4, 0, 0);
            Data(probe, 20, 0);
            probe.ContentSizeValue = 500f;
            probe.ExternalPadding = new Vector2(3f, 7f);
            probe.RunPositionChanged(new Vector2(0.2f, 0.5f));
            Assert.That(probe.Calls, Is.Empty);
            Assert.That(ring.All(o => o.ScrollCount == 0), Is.True);

            probe.DataCount = 10;
            probe.RunPositionChanged(new Vector2(0.2f, 0.5f));
            Assert.That(probe.AxisQueries.ToArray(), Is.EqualTo(new[] { new Vector2(0.2f, 0.5f) }));
            Assert.That(PositionNames(probe), Is.EqualTo(new[] { "O0", "O1", "O2" }));
            AssertReceived(ring[0], 40);
            AssertReceived(ring[2], 60);
            Assert.That(probe.IndexOfFirstEntryField, Is.EqualTo(3));
            Assert.That(probe.IndexOfFirstObject, Is.EqualTo(3));
            Assert.That(ring.Select(o => o.ScrollCount).ToArray(), Is.EqualTo(new[] { 1, 1, 1, 1 }));
        }

        // 0x1393acb..0x1393ad7: the forward move is capped by the entries left after the
        // ring; a backward target moves the whole distance.
        [Test]
        public void PositionChangedCapsForwardMovesAndMovesBackFully()
        {
            Probe probe = Build();
            Element[] ring = Ring(probe, 4, 0, 0);
            Data(probe, 20, 5);
            probe.ContentSizeValue = 500f;
            probe.RunPositionChanged(new Vector2(0f, 0f));
            Assert.That(PositionNames(probe), Is.EqualTo(new[] { "O0" }));
            AssertReceived(ring[0], 40);
            Assert.That(probe.IndexOfFirstEntryField, Is.EqualTo(1));
            Assert.That(probe.IndexOfFirstObject, Is.EqualTo(1));

            probe.ChildPositions.Clear();
            probe.IndexOfFirstEntryField = 5;
            probe.IndexOfFirstObject = 0;
            probe.RunPositionChanged(new Vector2(0f, 1f));
            Assert.That(PositionNames(probe), Is.EqualTo(new[] { "O3", "O0", "O1", "O2" }));
            Assert.That(probe.IndexOfFirstEntryField, Is.Zero);
            Assert.That(probe.IndexOfFirstObject, Is.EqualTo(3));
            Assert.That(ring.Select(o => o.ScrollCount).ToArray(), Is.EqualTo(new[] { 2, 2, 2, 2 }));
        }

        // 0x0600065d: object (first object + i) % count gets entry (first entry + i) % data
        // count; with objects but no data the remainder divides by zero.
        [Test]
        public void ReloadAllDataWrapsEntriesAndObjectsIndependently()
        {
            Probe probe = Build();
            Element[] ring = Ring(probe, 3, 1, 4);
            Data(probe, 5, 5);
            probe.ReloadAllData();
            AssertReceived(ring[1], 40);
            AssertReceived(ring[2], 0);
            AssertReceived(ring[0], 10);

            probe.DataCount = 0;
            Assert.Throws<DivideByZeroException>(() => probe.ReloadAllData());
        }

        // 0x0600065e: last to first, then the constructor indices (0, -1, 0, 0) at 0x1393689;
        // the data list itself is kept.
        [Test]
        public void ClearDestroysLastToFirstAndRestoresTheConstructorIndices()
        {
            Probe probe = Build();
            Element[] ring = Ring(probe, 3, 1, 4);
            Data(probe, 5, 5);
            IReadOnlyList<int> data = probe.Data;
            probe.Clear();
            Assert.That(probe.Destroyed.ToArray(), Is.EqualTo(new[] { ring[2], ring[1], ring[0] }));
            Assert.That(probe.Objects, Is.Empty);
            Assert.That(probe.IndexOfFirstEntryField, Is.Zero);
            Assert.That(probe.IndexOfFirstObject, Is.EqualTo(-1));
            Assert.That(probe.ObjectCount, Is.Zero);
            Assert.That(probe.DataCount, Is.Zero);
            Assert.That(probe.Data, Is.SameAs(data));
        }

        // 0x0600065b: growing honours resetScroll, but the remove path falls through to
        // ResetScroll at 0x1393370 even when resetScroll is false. Each call resizes, adds
        // or removes, refreshes and reloads every object.
        [Test]
        public void SetEntriesAlwaysResetsTheScrollAfterRemovingObjects()
        {
            Probe probe = Build();
            HLScrollRect scroll = AttachScroll(probe);
            probe.MaxObjectsRequired = 5;

            scroll.velocity = new Vector2(5f, 5f);
            probe.SetEntries(new List<int> { 1, 2, 3 }, false);
            Assert.That(scroll.velocity, Is.EqualTo(new Vector2(5f, 5f)), "growing with resetScroll false keeps the scroll");
            Assert.That(probe.ContentSizeWrites.ToArray(), Is.EqualTo(new[] { 191f }));
            Assert.That(Names(probe.Objects), Is.EqualTo(new[] { "Row0", "Row1", "Row2" }));
            Assert.That(probe.Objects.Select(o => o.Received.ToArray()).ToArray(),
                Is.EqualTo(new[] { new[] { 1, 1 }, new[] { 2, 2 }, new[] { 3, 3 } }));
            Assert.That(probe.AxisQueries.ToArray(), Is.EqualTo(new[] { scroll.normalizedPosition }));
            Assert.That(probe.Objects.Select(o => o.ScrollCount).ToArray(), Is.EqualTo(new[] { 1, 1, 1 }));
            Element first = probe.Objects[0];

            probe.SetEntries(new List<int> { 7 }, false);
            Assert.That(scroll.velocity, Is.EqualTo(Vector2.zero), "removing resets the scroll regardless");
            Assert.That(probe.ContentSizeWrites.Last(), Is.EqualTo(101f));
            Assert.That(probe.Objects.ToArray(), Is.EqualTo(new[] { first }));
            Assert.That(Names(probe.Destroyed), Is.EqualTo(new[] { "Row1", "Row2" }));
            AssertReceived(first, 1, 1, 7);

            scroll.velocity = new Vector2(5f, 5f);
            probe.SetEntries(new List<int> { 4, 5 }, true);
            Assert.That(scroll.velocity, Is.EqualTo(Vector2.zero));
            Element added = probe.Objects[0];
            Assert.That(probe.Objects.ToArray(), Is.EqualTo(new[] { added, first }));
            Assert.That(probe.IndexOfFirstObject, Is.EqualTo(1));
            AssertReceived(first, 1, 1, 7, 4);
            AssertReceived(added, 5, 5);
        }

        // 0x06000670/0x06000672: an inactive hierarchy starts nothing, so MarkDirty leaves
        // the flag clear and AdjustSizeToFitContents does not run.
        [Test]
        public void InactiveSetupAtEndOfFrameStartsNothingAndLeavesTheFlagClear()
        {
            Probe probe = Build();
            Assert.That(probe.RunSetupAtEndOfFrame(), Is.False);
            probe.RunMarkDirty();
            Assert.That(probe.IsDirty, Is.False);
            Assert.That(probe.ContentSizeWrites, Is.Empty);
        }

        // 0x0600067b: resize, two null yields (m_waitForEndOfFrame is never yielded), then
        // SetupContent, RefreshView and a clear dirty flag; Reset is unsupported.
        [Test]
        public void SetupCoroutineResizesWaitsTwoFramesThenBuilds()
        {
            Probe probe = Build();
            AttachScroll(probe);
            probe.IsDirty = true;
            IEnumerator routine = probe.RunCoroutineSetupAtEndOfFrame();
            Assert.That(routine.GetType().Name, Is.EqualTo("<Coroutine_SetupAtEndOfFrame>d__53"));
            Assert.That(probe.Calls, Is.Empty);

            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(routine.Current, Is.Null);
            Assert.That(probe.Calls.ToArray(), Is.EqualTo(new[] { "ContentSize" }));
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(routine.Current, Is.Null);
            Assert.That(probe.Calls.ToArray(), Is.EqualTo(new[] { "ContentSize" }));
            Assert.That(probe.IsDirty, Is.True);

            Assert.That(routine.MoveNext(), Is.False);
            Assert.That(probe.Calls.ToArray(), Is.EqualTo(new[] { "ContentSize", "ContentReady" }));
            Assert.That(probe.MaxObjectsRequired, Is.EqualTo(5));
            Assert.That(probe.IsDirty, Is.False);
            Assert.Throws<NotSupportedException>(() => routine.Reset());
        }

        // 0x0600065f/0x06000660: each method builds its own lambda, so OnDisable removes
        // nothing and every OnEnable adds another live listener.
        [Test]
        public void OnDisableNeverRemovesTheValueChangedListener()
        {
            Probe probe = Build();
            HLScrollRect scroll = AttachScroll(probe);
            Data(probe, 1, 1);
            probe.RunOnEnable();
            Assert.That(probe.ContentSizeWrites, Is.Empty, "the inactive hierarchy starts no setup");
            scroll.onValueChanged.Invoke(new Vector2(0.25f, 0.75f));
            Assert.That(probe.AxisQueries.Count, Is.EqualTo(1));

            probe.RunOnDisable();
            scroll.onValueChanged.Invoke(new Vector2(0.25f, 0.75f));
            Assert.That(probe.AxisQueries.Count, Is.EqualTo(2));

            probe.RunOnEnable();
            scroll.onValueChanged.Invoke(new Vector2(0.5f, 0.5f));
            Assert.That(probe.AxisQueries.Skip(2).ToArray(), Is.EqualTo(new[] { new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f) }));
        }
    }
}
