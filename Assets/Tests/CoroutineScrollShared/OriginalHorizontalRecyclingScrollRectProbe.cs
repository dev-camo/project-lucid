using Hardlight;
using UnityEngine;
using Element = ProjectLucid.Tests.OriginalRecyclingScrollRectElementProbe;

namespace ProjectLucid.Tests
{
    // Test-only concrete subclass of the abstract original HLHorizontalRecyclingScrollRect`2
    // (no original subclass exists). It adds nothing but public access to the original
    // overrides and protected fields. It lives in this runtime test assembly because Unity
    // refuses to attach a component whose script is Editor-only.
    public sealed class OriginalHorizontalRecyclingScrollRectProbe : HLHorizontalRecyclingScrollRect<Element, int>
    {
        public VerticalAlignment ChildAlignment { get => m_childAlignment; set => m_childAlignment = value; }
        public Element Prefab { get => m_listEntryPrefab; set => m_listEntryPrefab = value; }
        public HLScrollRect Scroll { get => m_scrollRect; set => m_scrollRect = value; }
        public RectTransform Content { get => m_content; set => m_content = value; }
        public Vector2 ExternalPadding { get => m_externalPadding; set => m_externalPadding = value; }
        public RectOffset InternalPadding { get => m_internalPadding; set => m_internalPadding = value; }

        public float StartPaddingValue => StartPadding;
        public float EndPaddingValue => EndPadding;
        public float ContentSizeValue { get => ContentSize; set => ContentSize = value; }
        public float ViewportSizeValue => ViewportSize;
        public float ElementSizeValue => ElementSize;
        public float RunGetNormalizedPositionOnAxis(Vector2 normalizedPosition) => GetNormalizedPositionOnAxis(normalizedPosition);
        public void RunSetChildPosition(Element child, float position) => SetChildPosition(ref child, position);
    }
}
