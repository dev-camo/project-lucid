using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectLucid.Tests
{
    // Test-only concrete subclass of the abstract original HLScrollRectContentFocus.
    // It records each CalculatePositionDelta (slot 5) and Refocus (slot 4) dispatch
    // and gives the fixture typed access to the protected original state; the
    // original subclasses (HLHorizontal/HLVerticalScrollRectContentFocus) are not
    // used, so only the base bodies are under test. It is a top-level class in its
    // own same-named file because AddComponent needs a MonoScript for it, and it
    // lives in this runtime test assembly because Unity refuses to attach a
    // component whose script belongs to an Editor-only assembly.
    public sealed class OriginalHLScrollRectContentFocusProbe : HLScrollRectContentFocus
    {
        public readonly List<string> Calls = new List<string>();

        protected override void CalculatePositionDelta(RectTransform child, Interaction interactionType, int index = 0)
        {
            Calls.Add("delta " + child.name + " " + (int)interactionType + " " + index);
        }

        public override void Refocus()
        {
            Calls.Add("refocus " + m_requiresRefocusing);
            base.Refocus();
        }

        public void RunAwake() => Awake();
        public void RunLateUpdate() => LateUpdate();
        public void RunInternalCalculatePositionDelta(RectTransform.Axis axis, float size, int interactionType, int index = 0) =>
            InternalCalculatePositionDelta(axis, size, (Interaction)interactionType, index);

        public ScrollRect ScrollRectField { get => m_scrollRect; set => m_scrollRect = value; }
        public RectTransform ContentHolder { get => m_scrollRectContentHolder; set => m_scrollRectContentHolder = value; }
        public RectTransform RemovalArea { get => m_removalArea; set => m_removalArea = value; }
        public float Spacing { get => m_spacingBetweenContentItems; set => m_spacingBetweenContentItems = value; }
        public int ChildLimit { get => m_childLimit; set => m_childLimit = value; }
        public HLScrollRect HLScrollRectField { get => m_hlScrollRect; set => m_hlScrollRect = value; }
        public bool RequiresRefocusing { get => m_requiresRefocusing; set => m_requiresRefocusing = value; }
        public bool RequiresLayoutRebuilding { get => m_requiresLayoutRebuilding; set => m_requiresLayoutRebuilding = value; }
        public bool PreserveVelocity { get => m_preserveVelocity; set => m_preserveVelocity = value; }
        public bool FocusEnabled => m_focusEnabled;
        public float AccumulatedPositionDelta { get => m_accumulatedPositionDelta; set => m_accumulatedPositionDelta = value; }
        public Vector2 ContentRelativeOffset { get => m_contentRelativeOffset; set => m_contentRelativeOffset = value; }
        public Vector2 PreviousScrollRectVelocity => m_previousScrollRectVelocity;
        public int[] Counters
        {
            get => new[] { m_childrenAddedToStart, m_childrenAddedToEnd, m_childrenRemovedFromStart, m_childrenRemovedFromEnd };
            set
            {
                m_childrenAddedToStart = value[0];
                m_childrenAddedToEnd = value[1];
                m_childrenRemovedFromStart = value[2];
                m_childrenRemovedFromEnd = value[3];
            }
        }
        public LinkedList<RectTransform> OrderedChildren => m_orderedChildren;
    }
}
