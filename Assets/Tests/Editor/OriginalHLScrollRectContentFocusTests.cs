using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Probe = ProjectLucid.Tests.OriginalHLScrollRectContentFocusProbe;

namespace ProjectLucid.Tests
{
    // Behaviour of the abstract Hardlight.HLScrollRectContentFocus (HLUnityUI.Runtime
    // 0600056d..57c) against the shipped x86-64 bodies, driven through a test-only
    // subclass that records the slot 4/5 dispatches. Every case builds its own
    // inactive, unsaved hierarchy and destroys it in TearDown; no static state is
    // touched. Expected values come from the native bodies cited per case.
    [TestFixture]
    public sealed class OriginalHLScrollRectContentFocusTests
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

        sealed class Rig
        {
            public GameObject Root;
            public ScrollRect Scroll;
            public RectTransform Viewport;
            public RectTransform Content;
            public RectTransform Removal;
            public Probe Focus;
        }

        // The root stays inactive so no engine callback runs; the original
        // callbacks are invoked explicitly.
        Rig Build(bool hlScrollRect, bool awake = true)
        {
            var rig = new Rig();
            rig.Root = new GameObject("ProjectLucid.HLScrollRectContentFocus.Owned", typeof(RectTransform));
            roots.Add(rig.Root);
            rig.Root.hideFlags = HideFlags.HideAndDontSave;
            rig.Root.SetActive(false);
            rig.Scroll = hlScrollRect ? rig.Root.AddComponent<HLScrollRect>() : rig.Root.AddComponent<ScrollRect>();
            rig.Focus = rig.Root.AddComponent<Probe>();
            rig.Viewport = NewRect("Viewport", rig.Root.transform, new Vector2(100f, 100f));
            rig.Content = NewRect("Content", rig.Viewport, new Vector2(100f, 100f));
            rig.Removal = NewRect("Removal", rig.Root.transform, new Vector2(10f, 10f));
            rig.Removal.localPosition = new Vector3(300f, -40f, 0f);
            rig.Scroll.viewport = rig.Viewport;
            rig.Scroll.content = rig.Content;
            rig.Focus.RemovalArea = rig.Removal;
            if (awake) rig.Focus.RunAwake();
            return rig;
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

        static RectTransform Child(Rig rig, string name) => NewRect(name, rig.Root.transform, new Vector2(40f, 40f));
        static string[] Order(Probe focus) => focus.OrderedChildren.Select(child => child.name).ToArray();

        static void Near(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(1e-3f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(1e-3f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(1e-3f));
        }

        static Vector2 ContentStartPosition(ScrollRect scroll) => (Vector2)typeof(ScrollRect)
            .GetField("m_ContentStartPosition", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scroll);

        static void SetContentStartPosition(ScrollRect scroll, Vector2 value) => typeof(ScrollRect)
            .GetField("m_ContentStartPosition", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(scroll, value);

        // dump.cs TypeDefIndex 12026..12028: enum literals, the four protected
        // constants and the instance field order (original-derived declarations).
        [Test]
        public void DeclarationsKeepTheDumpedEnumsConstantsAndFieldOrder()
        {
            Type type = typeof(HLScrollRectContentFocus);
            Assert.That(type.IsAbstract, Is.True);
            Assert.That(type.GetCustomAttributes<RequireComponent>().Single().m_Type0, Is.EqualTo(typeof(ScrollRect)));

            Type interaction = type.GetNestedType("Interaction", BindingFlags.NonPublic);
            Assert.That(interaction.IsNestedFamily, Is.True);
            CollectionAssert.AreEqual(new[] { "AddedToStart", "AddedToEnd", "AddedAtIndex", "RemovedFromStart", "RemovedFromEnd", "RemovedAtIndex" },
                Enum.GetNames(interaction));
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5 }, Enum.GetValues(interaction).Cast<object>().Select(value => Convert.ToInt32(value)).ToArray());
            CollectionAssert.AreEqual(new[] { "Start", "End" }, Enum.GetNames(typeof(HLScrollRectContentFocus.Extent)));
            Assert.That((int)HLScrollRectContentFocus.Extent.End, Is.EqualTo(1));

            FieldInfo[] constants = type.GetFields(Declared).Where(field => field.IsLiteral).ToArray();
            CollectionAssert.AreEqual(new[] { "StartInsertConstant", "EndInsertConstant", "RemovalConstant", "AxisFlipConstant" },
                constants.Select(field => field.Name).ToArray());
            CollectionAssert.AreEqual(new object[] { 0f, 1f, -1f, -1f }, constants.Select(field => field.GetRawConstantValue()).ToArray());
            Assert.That(constants.All(field => field.IsFamily), Is.True);

            CollectionAssert.AreEqual(new[]
            {
                "OnChildrenAddedToStart", "OnChildrenAddedToEnd", "OnChildrenRemovedFromStart", "OnChildrenRemovedFromEnd",
                "m_scrollRect", "m_scrollRectContentHolder", "m_removalArea", "m_spacingBetweenContentItems", "m_childLimit",
                "m_hlScrollRect", "m_requiresRefocusing", "m_requiresLayoutRebuilding", "m_preserveVelocity", "m_focusEnabled",
                "m_accumulatedPositionDelta", "m_contentRelativeOffset", "m_previousScrollRectVelocity",
                "m_childrenAddedToStart", "m_childrenAddedToEnd", "m_childrenRemovedFromStart", "m_childrenRemovedFromEnd",
                "m_orderedChildren",
            }, type.GetFields(Declared).Where(field => !field.IsStatic).Select(field => field.Name).ToArray());
            CollectionAssert.AreEqual(new[] { "m_scrollRect", "m_scrollRectContentHolder", "m_removalArea", "m_spacingBetweenContentItems", "m_childLimit" },
                type.GetFields(Declared).Where(field => field.IsDefined(typeof(SerializeField), false)).Select(field => field.Name).ToArray());
        }

        // .ctor 0x1b66f40 only allocates the list and zeroes both vectors; events
        // stay null and focus starts disabled. Awake is not a constructor step.
        [Test]
        public void ConstructorAllocatesOnlyTheChildListAndFocusStartsDisabled()
        {
            Rig rig = Build(hlScrollRect: true, awake: false);
            Probe focus = rig.Focus;
            Assert.That(focus.OrderedChildren, Is.Not.Null);
            Assert.That(focus.OrderedChildren.Count, Is.Zero);
            Assert.That(focus.OnChildrenAddedToStart, Is.Null);
            Assert.That(focus.OnChildrenAddedToEnd, Is.Null);
            Assert.That(focus.OnChildrenRemovedFromStart, Is.Null);
            Assert.That(focus.OnChildrenRemovedFromEnd, Is.Null);
            Assert.That(focus.ScrollRectField == null, Is.True);
            Assert.That(focus.ContentHolder == null, Is.True);
            Assert.That(focus.HLScrollRectField == null, Is.True);
            Assert.That(focus.FocusEnabled, Is.False);
            Assert.That(focus.ChildLimit, Is.Zero);
            Assert.That(focus.ContentRelativeOffset, Is.EqualTo(Vector2.zero));
            Assert.That(focus.PreviousScrollRectVelocity, Is.EqualTo(Vector2.zero));
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0 }, focus.Counters);

            // 0x1b664a0 stores the argument as given, both ways.
            focus.EnableFocus(true);
            Assert.That(focus.FocusEnabled, Is.True);
            focus.EnableFocus(false);
            Assert.That(focus.FocusEnabled, Is.False);
        }

        // Awake 0x1b662e0: missing references come from GetComponent<ScrollRect>
        // and its content; m_hlScrollRect is an as-cast of the assigned field.
        [Test]
        public void AwakeFillsMissingReferencesAndCastsTheAssignedScrollRect()
        {
            Rig hl = Build(hlScrollRect: true);
            Assert.That(hl.Focus.ScrollRectField, Is.SameAs(hl.Scroll));
            Assert.That(hl.Focus.ContentHolder, Is.SameAs(hl.Content));
            Assert.That(hl.Focus.HLScrollRectField, Is.SameAs(hl.Scroll));

            Rig plain = Build(hlScrollRect: false);
            Assert.That(plain.Focus.ScrollRectField, Is.SameAs(plain.Scroll));
            Assert.That(plain.Focus.ContentHolder, Is.SameAs(plain.Content));
            Assert.That(plain.Focus.HLScrollRectField == null, Is.True);

            // Assigned references win: a plain ScrollRect on another object leaves
            // m_hlScrollRect null although this object carries an HLScrollRect.
            Rig assigned = Build(hlScrollRect: true, awake: false);
            RectTransform holder = NewRect("AssignedHolder", assigned.Root.transform, Vector2.one);
            assigned.Focus.ScrollRectField = plain.Scroll;
            assigned.Focus.ContentHolder = holder;
            assigned.Focus.RunAwake();
            Assert.That(assigned.Focus.ScrollRectField, Is.SameAs(plain.Scroll));
            Assert.That(assigned.Focus.ContentHolder, Is.SameAs(holder));
            Assert.That(assigned.Focus.HLScrollRectField == null, Is.True);

            // A missing holder is read from the assigned scroll rect, not this one.
            Rig borrowed = Build(hlScrollRect: true, awake: false);
            borrowed.Focus.ScrollRectField = plain.Scroll;
            borrowed.Focus.RunAwake();
            Assert.That(borrowed.Focus.ContentHolder, Is.SameAs(plain.Content));
        }

        // AddContentChildToStart 0x1b664b0: parent without keeping world position,
        // sibling 0, and only with focus the slot 5 call, the three flags and the
        // start counter.
        [Test]
        public void AddToStartParentsAtSiblingZeroAndRecordsOnlyWithFocus()
        {
            Rig rig = Build(hlScrollRect: true);
            Probe focus = rig.Focus;
            rig.Content.localPosition = new Vector3(100f, 0f, 0f);
            NewRect("Existing", rig.Content, Vector2.one);
            RectTransform a = Child(rig, "A");
            a.localPosition = new Vector3(5f, 6f, 0f);

            Assert.That(focus.AddContentChildToStart(a) == null, Is.True);
            Assert.That(a.parent, Is.SameAs(rig.Content));
            Assert.That(a.GetSiblingIndex(), Is.Zero);
            Near(a.localPosition, new Vector3(5f, 6f, 0f));
            CollectionAssert.AreEqual(new[] { "A" }, Order(focus));
            CollectionAssert.IsEmpty(focus.Calls);
            Assert.That(focus.RequiresRefocusing || focus.RequiresLayoutRebuilding || focus.PreserveVelocity, Is.False);
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0 }, focus.Counters);

            focus.EnableFocus(true);
            RectTransform b = Child(rig, "B");
            Assert.That(focus.AddContentChildToStart(b) == null, Is.True);
            Assert.That(b.GetSiblingIndex(), Is.Zero);
            Assert.That(a.GetSiblingIndex(), Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { "B", "A" }, Order(focus));
            CollectionAssert.AreEqual(new[] { "delta B 0 0" }, focus.Calls);
            Assert.That(focus.RequiresRefocusing && focus.RequiresLayoutRebuilding && focus.PreserveVelocity, Is.True);
            CollectionAssert.AreEqual(new[] { 1, 0, 0, 0 }, focus.Counters);
        }

        // AddContentChildToStart at the limit evicts through RemoveContentChildFromEnd
        // (0x1b665b0) before inserting, and returns the evicted child.
        [Test]
        public void AddToStartAtTheLimitEvictsAndReturnsTheEndChild()
        {
            Rig rig = Build(hlScrollRect: true);
            Probe focus = rig.Focus;
            focus.EnableFocus(true);
            focus.ChildLimit = 1;
            RectTransform a = Child(rig, "A");
            RectTransform b = Child(rig, "B");
            Assert.That(focus.AddContentChildToStart(a) == null, Is.True);
            Assert.That(focus.AddContentChildToStart(b), Is.SameAs(a));
            CollectionAssert.AreEqual(new[] { "B" }, Order(focus));
            Assert.That(a.gameObject.activeSelf, Is.False);
            Assert.That(a.parent, Is.SameAs(rig.Removal));
            CollectionAssert.AreEqual(new[] { "delta A 0 0", "delta A 4 0", "delta B 0 0" }, focus.Calls);
            CollectionAssert.AreEqual(new[] { 2, 0, 0, 1 }, focus.Counters);
        }

        // AddContentChildToEnd 0x1b66680: last sibling of the holder; at the limit
        // RemoveContentChildFromStart (0x1b66790) runs first. A limit of 0 never evicts.
        [Test]
        public void AddToEndUsesTheLastSiblingAndEvictsTheStartChildAtTheLimit()
        {
            Rig rig = Build(hlScrollRect: true);
            Probe focus = rig.Focus;
            focus.EnableFocus(true);
            RectTransform a = Child(rig, "A");
            RectTransform b = Child(rig, "B");
            RectTransform c = Child(rig, "C");
            NewRect("Existing", rig.Content, Vector2.one);
            Assert.That(focus.AddContentChildToEnd(a) == null, Is.True);
            Assert.That(a.GetSiblingIndex(), Is.EqualTo(1));
            Assert.That(focus.AddContentChildToEnd(b) == null, Is.True);
            focus.ChildLimit = 2;
            Assert.That(focus.AddContentChildToEnd(c), Is.SameAs(a));
            Assert.That(c.GetSiblingIndex(), Is.EqualTo(rig.Content.childCount - 1));
            CollectionAssert.AreEqual(new[] { "B", "C" }, Order(focus));
            Assert.That(a.gameObject.activeSelf, Is.False);
            Assert.That(a.parent, Is.SameAs(rig.Removal));
            CollectionAssert.AreEqual(new[] { "delta A 1 0", "delta B 1 0", "delta A 3 0", "delta C 1 0" }, focus.Calls);
            CollectionAssert.AreEqual(new[] { 0, 3, 1, 0 }, focus.Counters);

            focus.ChildLimit = 0;
            RectTransform d = Child(rig, "D");
            Assert.That(focus.AddContentChildToEnd(d) == null, Is.True);
            CollectionAssert.AreEqual(new[] { "B", "C", "D" }, Order(focus));
        }

        // RemoveContentChildFromStart/End (0x1b66790/0x1b665b0): deactivate, move
        // under the removal area keeping world position, and record only with focus.
        // An empty list throws from LinkedList.RemoveFirst/RemoveLast.
        [Test]
        public void RemoveFromStartAndEndKeepWorldPositionAndRecordOnlyWithFocus()
        {
            Rig rig = Build(hlScrollRect: true);
            Probe focus = rig.Focus;
            RectTransform a = Child(rig, "A");
            RectTransform b = Child(rig, "B");
            RectTransform c = Child(rig, "C");
            focus.AddContentChildToEnd(a);
            focus.AddContentChildToEnd(b);
            focus.AddContentChildToEnd(c);
            a.anchoredPosition = new Vector2(-30f, 12f);
            Vector3 world = a.position;

            Assert.That(focus.RemoveContentChildFromStart(), Is.SameAs(a));
            Assert.That(a.gameObject.activeSelf, Is.False);
            Assert.That(a.parent, Is.SameAs(rig.Removal));
            Near(a.position, world);
            CollectionAssert.AreEqual(new[] { "B", "C" }, Order(focus));
            CollectionAssert.IsEmpty(focus.Calls);
            Assert.That(focus.RequiresRefocusing || focus.RequiresLayoutRebuilding || focus.PreserveVelocity, Is.False);
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0 }, focus.Counters);

            focus.EnableFocus(true);
            Assert.That(focus.RemoveContentChildFromEnd(), Is.SameAs(c));
            Assert.That(c.gameObject.activeSelf, Is.False);
            Assert.That(c.parent, Is.SameAs(rig.Removal));
            CollectionAssert.AreEqual(new[] { "B" }, Order(focus));
            CollectionAssert.AreEqual(new[] { "delta C 4 0" }, focus.Calls);
            Assert.That(focus.RequiresRefocusing && focus.RequiresLayoutRebuilding && focus.PreserveVelocity, Is.True);
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 1 }, focus.Counters);

            Assert.That(focus.RemoveContentChildFromStart(), Is.SameAs(b));
            CollectionAssert.AreEqual(new[] { 0, 0, 1, 1 }, focus.Counters);
            Assert.Throws<InvalidOperationException>(() => focus.RemoveContentChildFromStart());
            Assert.Throws<InvalidOperationException>(() => focus.RemoveContentChildFromEnd());
        }

        // RemoveContentChild 0x1b66850: the index search loops while node.Next is
        // non-null, so the last child (and a single child) report index 0; a
        // sequential search would report 2 for D. No removal counter changes, and
        // a child that is not listed is left untouched.
        [Test]
        public void RemoveContentChildReportsTheSearchIndexAndNeverSearchesTheLastNode()
        {
            Rig rig = Build(hlScrollRect: true);
            Probe focus = rig.Focus;
            focus.EnableFocus(true);
            RectTransform a = Child(rig, "A");
            RectTransform b = Child(rig, "B");
            RectTransform c = Child(rig, "C");
            RectTransform d = Child(rig, "D");
            foreach (RectTransform child in new[] { a, b, c, d })
                focus.AddContentChildToEnd(child);
            focus.Calls.Clear();
            focus.RequiresRefocusing = focus.RequiresLayoutRebuilding = focus.PreserveVelocity = false;

            RectTransform stranger = Child(rig, "Stranger");
            Assert.That(focus.RemoveContentChild(stranger), Is.False);
            Assert.That(stranger.gameObject.activeSelf, Is.True);
            Assert.That(stranger.parent, Is.SameAs(rig.Root.transform));
            CollectionAssert.IsEmpty(focus.Calls);
            Assert.That(focus.RequiresRefocusing, Is.False);

            Assert.That(focus.RemoveContentChild(b), Is.True);
            Assert.That(b.gameObject.activeSelf, Is.False);
            Assert.That(b.parent, Is.SameAs(rig.Removal));
            Assert.That(focus.RequiresRefocusing && focus.RequiresLayoutRebuilding && focus.PreserveVelocity, Is.True);
            Assert.That(focus.RemoveContentChild(d), Is.True);
            Assert.That(focus.RemoveContentChild(a), Is.True);
            CollectionAssert.AreEqual(new[] { "C" }, Order(focus));
            Assert.That(focus.RemoveContentChild(c), Is.True);
            CollectionAssert.AreEqual(new[] { "delta B 5 1", "delta D 5 0", "delta A 5 0", "delta C 5 0" }, focus.Calls);
            CollectionAssert.AreEqual(new[] { 0, 4, 0, 0 }, focus.Counters);

            // The emptied list throws from the first node.Next (0x1b668c8 on a
            // null head) before anything is removed or recorded.
            Assert.Throws<NullReferenceException>(() => focus.RemoveContentChild(c));
            Assert.That(focus.Calls.Count, Is.EqualTo(4));

            // Without focus the child is still deactivated and reparented.
            focus.EnableFocus(false);
            focus.AddContentChildToEnd(stranger);
            focus.Calls.Clear();
            Assert.That(focus.RemoveContentChild(stranger), Is.True);
            Assert.That(stranger.gameObject.activeSelf, Is.False);
            Assert.That(stranger.parent, Is.SameAs(rig.Removal));
            CollectionAssert.IsEmpty(focus.Calls);
        }

        // InternalCalculatePositionDelta 0x1b66e60 with its five-entry jump table
        // and the -1.0f / sign-mask constants: holder pivot share minus the start
        // (0) or end (1) constant, negated for removals, flipped for Horizontal,
        // times (size + spacing), added to the accumulator. Index interactions and
        // values past the table add 0; the index argument is ignored.
        [Test]
        public void InternalCalculatePositionDeltaUsesThePivotShareAndFlipsHorizontal()
        {
            Rig rig = Build(hlScrollRect: true);
            Probe focus = rig.Focus;
            rig.Content.pivot = new Vector2(0.25f, 0.75f);
            focus.Spacing = 2f;
            var expected = new Dictionary<RectTransform.Axis, float[]>
            {
                [RectTransform.Axis.Vertical] = new[] { 0.75f, -0.25f, 0f, -0.75f, 0.25f, 0f, 0f },
                [RectTransform.Axis.Horizontal] = new[] { -0.25f, 0.75f, 0f, 0.25f, -0.75f, 0f, 0f },
            };
            foreach (KeyValuePair<RectTransform.Axis, float[]> axis in expected)
            {
                for (int interaction = 0; interaction < axis.Value.Length; interaction++)
                {
                    focus.AccumulatedPositionDelta = 0.5f;
                    focus.RunInternalCalculatePositionDelta(axis.Key, 10f, interaction, 7);
                    Assert.That(focus.AccumulatedPositionDelta, Is.EqualTo(0.5f + 12f * axis.Value[interaction]),
                        axis.Key + " interaction " + interaction);
                }
            }

            focus.AccumulatedPositionDelta = 0f;
            focus.RunInternalCalculatePositionDelta(RectTransform.Axis.Vertical, 10f, 0);
            focus.RunInternalCalculatePositionDelta(RectTransform.Axis.Vertical, 4f, 1);
            Assert.That(focus.AccumulatedPositionDelta, Is.EqualTo(12f * 0.75f + 6f * -0.25f));
            CollectionAssert.IsEmpty(focus.Calls);
        }

        // Refocus 0x1b66a30: needs both the pending flag and focus; then rebuilds
        // when flagged, offsets the holder and (HLScrollRect only) the inherited
        // content start, clears the accumulator and flag but not m_preserveVelocity,
        // and publishes non-zero counters in start/end, added/removed order.
        [Test]
        public void RefocusAppliesTheOffsetAndPublishesCountersOnlyWhenPendingAndEnabled()
        {
            Rig rig = Build(hlScrollRect: true);
            Probe focus = rig.Focus;
            var log = new List<string>();
            focus.OnChildrenAddedToStart += count => log.Add("added start " + count);
            focus.OnChildrenAddedToEnd += count => log.Add("added end " + count);
            focus.OnChildrenRemovedFromStart += count => log.Add("removed start " + count);
            focus.OnChildrenRemovedFromEnd += count => log.Add("removed end " + count);
            rig.Content.anchoredPosition = new Vector2(3f, 4f);
            SetContentStartPosition(rig.Scroll, new Vector2(1f, 2f));
            focus.ContentRelativeOffset = new Vector2(10f, -20f);
            focus.AccumulatedPositionDelta = 5f;
            focus.Counters = new[] { 2, 0, 3, 1 };
            focus.RequiresLayoutRebuilding = true;
            focus.PreserveVelocity = true;

            focus.RequiresRefocusing = true;
            focus.Refocus();
            focus.EnableFocus(true);
            focus.RequiresRefocusing = false;
            focus.Refocus();
            Near(rig.Content.anchoredPosition, new Vector2(3f, 4f));
            Assert.That(focus.AccumulatedPositionDelta, Is.EqualTo(5f));
            Assert.That(focus.RequiresLayoutRebuilding, Is.True);
            CollectionAssert.AreEqual(new[] { 2, 0, 3, 1 }, focus.Counters);
            CollectionAssert.IsEmpty(log);

            focus.RequiresRefocusing = true;
            focus.Refocus();
            Near(rig.Content.anchoredPosition, new Vector2(13f, -16f));
            Assert.That(ContentStartPosition(rig.Scroll), Is.EqualTo(new Vector2(11f, -18f)));
            Assert.That(focus.AccumulatedPositionDelta, Is.Zero);
            Assert.That(focus.RequiresRefocusing, Is.False);
            Assert.That(focus.RequiresLayoutRebuilding, Is.False);
            Assert.That(focus.PreserveVelocity, Is.True);
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0 }, focus.Counters);
            CollectionAssert.AreEqual(new[] { "added start 2", "removed start 3", "removed end 1" }, log);

            focus.Refocus();
            Near(rig.Content.anchoredPosition, new Vector2(13f, -16f));
            Assert.That(log.Count, Is.EqualTo(3));

            // A plain ScrollRect only moves the holder; null events are tolerated
            // by the FastAction Invoke extension and the counters still reset.
            Rig plain = Build(hlScrollRect: false);
            plain.Focus.EnableFocus(true);
            plain.Content.anchoredPosition = Vector2.zero;
            SetContentStartPosition(plain.Scroll, new Vector2(1f, 2f));
            plain.Focus.ContentRelativeOffset = new Vector2(0f, 7f);
            plain.Focus.Counters = new[] { 1, 1, 1, 1 };
            plain.Focus.RequiresRefocusing = true;
            plain.Focus.Refocus();
            Near(plain.Content.anchoredPosition, new Vector2(0f, 7f));
            Assert.That(ContentStartPosition(plain.Scroll), Is.EqualTo(new Vector2(1f, 2f)));
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0 }, plain.Focus.Counters);
        }

        // A fitted rect lays itself out at its LayoutElement preferred size only
        // when LayoutRebuilder rebuilds it; it starts at a stale 40 x 40.
        static RectTransform Fitted(string name, Transform parent, float width, float height)
        {
            RectTransform rect = NewRect(name, parent, new Vector2(40f, 40f));
            LayoutElement element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = height;
            ContentSizeFitter fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rect;
        }

        // ForceRebuildLayoutImmediate on the new child (0x1b66569 and 0x1b66745,
        // before the focus test) and on the holder (RebuildContentLayout 0x1b66a17,
        // inlined in Refocus at 0x1b66ab9 only while m_requiresLayoutRebuilding).
        // LayoutRebuilder skips inactive behaviours, so the holder and children
        // live under a separate active owned root; the probe's own root stays
        // inactive. Hand-parented and unflagged controls keep the stale size.
        [Test]
        public void AddedChildrenAndTheFlaggedHolderAreLaidOutImmediately()
        {
            Rig rig = Build(hlScrollRect: true);
            Probe focus = rig.Focus;
            var active = new GameObject("ProjectLucid.HLScrollRectContentFocus.ActiveOwned", typeof(RectTransform));
            roots.Add(active);
            active.hideFlags = HideFlags.HideAndDontSave;
            RectTransform holder = Fitted("Holder", active.transform, 150f, 60f);
            focus.ContentHolder = holder;

            RectTransform manual = Fitted("Manual", active.transform, 90f, 10f);
            manual.SetParent(holder, false);
            Near(manual.sizeDelta, new Vector2(40f, 40f));

            RectTransform start = Fitted("Start", active.transform, 77f, 33f);
            focus.AddContentChildToStart(start);
            Near(start.sizeDelta, new Vector2(77f, 33f));
            RectTransform end = Fitted("End", active.transform, 55f, 22f);
            focus.AddContentChildToEnd(end);
            Near(end.sizeDelta, new Vector2(55f, 22f));
            Near(holder.sizeDelta, new Vector2(40f, 40f));
            CollectionAssert.IsEmpty(focus.Calls);

            focus.EnableFocus(true);
            focus.RequiresRefocusing = true;
            focus.RequiresLayoutRebuilding = false;
            focus.Refocus();
            Near(holder.sizeDelta, new Vector2(40f, 40f));
            Near(manual.sizeDelta, new Vector2(40f, 40f));

            focus.RequiresRefocusing = true;
            focus.RequiresLayoutRebuilding = true;
            focus.Refocus();
            Near(holder.sizeDelta, new Vector2(150f, 60f));
            Near(manual.sizeDelta, new Vector2(90f, 10f));
            Assert.That(focus.RequiresLayoutRebuilding, Is.False);

            holder.GetComponent<LayoutElement>().preferredWidth = 120f;
            focus.RequiresLayoutRebuilding = true;
            focus.RebuildContentLayout();
            Near(holder.sizeDelta, new Vector2(120f, 60f));
            Assert.That(focus.RequiresLayoutRebuilding, Is.False);
        }

        // LateUpdate 0x1b66f20 tail-dispatches slot 4 only while pending. OnDisable
        // 0x1b66460 dispatches it while pending and then clears the flag and
        // accumulator even if Refocus did nothing; the rebuild flag always clears.
        [Test]
        public void LateUpdateAndOnDisableDispatchRefocusThroughTheVirtualSlot()
        {
            Rig rig = Build(hlScrollRect: true);
            Probe focus = rig.Focus;
            MethodInfo onDisable = typeof(HLScrollRectContentFocus).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(onDisable.IsPrivate, Is.True);

            focus.RunLateUpdate();
            CollectionAssert.IsEmpty(focus.Calls);
            focus.RequiresRefocusing = true;
            focus.RunLateUpdate();
            CollectionAssert.AreEqual(new[] { "refocus True" }, focus.Calls);
            Assert.That(focus.RequiresRefocusing, Is.True);

            focus.AccumulatedPositionDelta = 5f;
            focus.RequiresLayoutRebuilding = true;
            onDisable.Invoke(focus, null);
            CollectionAssert.AreEqual(new[] { "refocus True", "refocus True" }, focus.Calls);
            Assert.That(focus.RequiresRefocusing, Is.False);
            Assert.That(focus.AccumulatedPositionDelta, Is.Zero);
            Assert.That(focus.RequiresLayoutRebuilding, Is.False);

            focus.AccumulatedPositionDelta = 5f;
            focus.RequiresLayoutRebuilding = true;
            onDisable.Invoke(focus, null);
            Assert.That(focus.Calls.Count, Is.EqualTo(2));
            Assert.That(focus.AccumulatedPositionDelta, Is.EqualTo(5f));
            Assert.That(focus.RequiresLayoutRebuilding, Is.False);
        }

        // SetScrollRectNormalisedPosition 0x1b66bf0 forwards only to an HLScrollRect.
        [Test]
        public void SetScrollRectNormalisedPositionMovesOnlyAnHLScrollRect()
        {
            foreach (bool hl in new[] { true, false })
            {
                Rig rig = Build(hl);
                rig.Viewport.sizeDelta = new Vector2(200f, 100f);
                rig.Content.sizeDelta = new Vector2(400f, 300f);
                rig.Scroll.movementType = ScrollRect.MovementType.Unrestricted;
                rig.Scroll.normalizedPosition = new Vector2(0.9f, 0.2f);
                rig.Focus.SetScrollRectNormalisedPosition(new Vector2(0.1f, 0.8f));
                Vector2 position = rig.Scroll.normalizedPosition;
                Vector2 expected = hl ? new Vector2(0.1f, 0.8f) : new Vector2(0.9f, 0.2f);
                Assert.That(position.x, Is.EqualTo(expected.x).Within(1e-3f), "HLScrollRect " + hl);
                Assert.That(position.y, Is.EqualTo(expected.y).Within(1e-3f), "HLScrollRect " + hl);
            }
        }

        // IsChildVisibleToViewport 0x1b66c70: Start walks Next and End walks
        // Previous, staying put at the far end; the test is Rect.Overlaps of the
        // child and viewport world corners, so partial overlap counts and a shared
        // edge does not. An empty list is not visible.
        [Test]
        public void IsChildVisibleToViewportWalksInwardAndRequiresStrictOverlap()
        {
            Rig rig = Build(hlScrollRect: false);
            Probe focus = rig.Focus;
            Assert.That(focus.IsChildVisibleToViewport(HLScrollRectContentFocus.Extent.Start), Is.False);
            Assert.That(focus.IsChildVisibleToViewport(HLScrollRectContentFocus.Extent.End), Is.False);

            // Viewport spans -50..50 on both axes; children are 40 x 40.
            var placed = new[] { ("Outside", new Vector2(-200f, 0f)), ("Edge", new Vector2(70f, 0f)), ("Partial", new Vector2(40f, -40f)) };
            foreach ((string name, Vector2 position) in placed)
            {
                RectTransform child = Child(rig, name);
                focus.AddContentChildToEnd(child);
                child.anchoredPosition = position;
            }

            Assert.That(focus.IsChildVisibleToViewport(HLScrollRectContentFocus.Extent.Start), Is.False);
            Assert.That(focus.IsChildVisibleToViewport(HLScrollRectContentFocus.Extent.Start, 1), Is.False);
            Assert.That(focus.IsChildVisibleToViewport(HLScrollRectContentFocus.Extent.Start, 2), Is.True);
            Assert.That(focus.IsChildVisibleToViewport(HLScrollRectContentFocus.Extent.Start, 99), Is.True);
            Assert.That(focus.IsChildVisibleToViewport(HLScrollRectContentFocus.Extent.End), Is.True);
            Assert.That(focus.IsChildVisibleToViewport(HLScrollRectContentFocus.Extent.End, 1), Is.False);
            Assert.That(focus.IsChildVisibleToViewport(HLScrollRectContentFocus.Extent.End, 99), Is.False);
            Assert.That(focus.IsChildVisibleToViewport(HLScrollRectContentFocus.Extent.End, -3), Is.True);

            // Inside on x but past the top edge on y.
            RectTransform above = Child(rig, "Above");
            focus.AddContentChildToEnd(above);
            above.anchoredPosition = new Vector2(0f, 100f);
            Assert.That(focus.IsChildVisibleToViewport(HLScrollRectContentFocus.Extent.End), Is.False);
            Assert.That(focus.IsChildVisibleToViewport(HLScrollRectContentFocus.Extent.End, 1), Is.True);
            above.anchoredPosition = new Vector2(0f, 60f);
            Assert.That(focus.IsChildVisibleToViewport(HLScrollRectContentFocus.Extent.End), Is.True);
        }
    }
}
