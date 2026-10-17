using System.Collections;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Element = ProjectLucid.Tests.OriginalRecyclingScrollRectElementProbe;

namespace ProjectLucid.Tests
{
    // Test-only concrete subclass of the abstract original HLRecyclingScrollRect`2 (no
    // original subclass exists). The size getters return fixture-set values;
    // set_ContentSize (slot 20), GetNormalizedPositionOnAxis (23), SetChildPosition (24)
    // and ContentReady (27) record their dispatches, and Destroy (26) records the object
    // instead of calling Object.Destroy, which edit mode refuses, unless
    // DestroyThroughBase is set. Everything else is the original
    // base body, reached through the public Run* wrappers. It lives in this runtime test
    // assembly because Unity refuses to attach a component whose script is Editor-only.
    public sealed class OriginalRecyclingScrollRectProbe : HLRecyclingScrollRect<Element, int>
    {
        public struct ChildPosition
        {
            public Element Child;
            public string Name;
            public float Position;
        }

        public float StartPaddingValue;
        public float EndPaddingValue;
        public float ContentSizeValue;
        public float ViewportSizeValue;
        public float ElementSizeValue;
        public bool DestroyThroughBase;

        public readonly List<string> Calls = new List<string>();
        public readonly List<float> ContentSizeWrites = new List<float>();
        public readonly List<Vector2> AxisQueries = new List<Vector2>();
        public readonly List<ChildPosition> ChildPositions = new List<ChildPosition>();
        public readonly List<Element> Destroyed = new List<Element>();

        protected override float StartPadding => StartPaddingValue;
        protected override float EndPadding => EndPaddingValue;

        protected override float ContentSize
        {
            get => ContentSizeValue;
            set
            {
                Calls.Add("ContentSize");
                ContentSizeWrites.Add(value);
                ContentSizeValue = value;
            }
        }

        protected override float ViewportSize => ViewportSizeValue;
        protected override float ElementSize => ElementSizeValue;

        protected override float GetNormalizedPositionOnAxis(Vector2 normalizedPosition)
        {
            Calls.Add("GetNormalizedPositionOnAxis");
            AxisQueries.Add(normalizedPosition);
            return normalizedPosition.y;
        }

        protected override void SetChildPosition(ref Element child, float position)
        {
            Calls.Add("SetChildPosition");
            ChildPositions.Add(new ChildPosition { Child = child, Name = child.name, Position = position });
        }

        protected override void Destroy(Element objectToDestroy)
        {
            Calls.Add("Destroy");
            Destroyed.Add(objectToDestroy);
            if (DestroyThroughBase)
                base.Destroy(objectToDestroy);
        }

        protected override void ContentReady()
        {
            Calls.Add("ContentReady");
            base.ContentReady();
        }

        public Element Prefab { get => m_listEntryPrefab; set => m_listEntryPrefab = value; }
        public HLScrollRect Scroll { get => m_scrollRect; set => m_scrollRect = value; }
        public RectTransform Content { get => m_content; set => m_content = value; }
        public Vector2 ExternalPadding { get => m_externalPadding; set => m_externalPadding = value; }
        public RectOffset InternalPadding { get => m_internalPadding; set => m_internalPadding = value; }
        public float Spacing { get => m_spacing; set => m_spacing = value; }
        public bool RegenerateOnSizeChanged { get => m_regenerateOnSizeChanged; set => m_regenerateOnSizeChanged = value; }
        public float Allowance { get => m_allowance; set => m_allowance = value; }
        public List<Element> Objects => m_objects;
        public IReadOnlyList<int> Data { get => m_data; set => m_data = value; }
        public int IndexOfFirstEntryField { get => m_indexOfFirstEntry; set => m_indexOfFirstEntry = value; }
        public int IndexOfFirstObject { get => m_indexOfFirstObject; set => m_indexOfFirstObject = value; }
        public int ObjectCount { get => m_objectCount; set => m_objectCount = value; }
        public int DataCount { get => m_dataCount; set => m_dataCount = value; }
        public int ResizeOffset { get => m_resizeOffset; set => m_resizeOffset = value; }
        public int MaxObjectsRequired { get => m_maxObjectsRequired; set => m_maxObjectsRequired = value; }
        public WaitForEndOfFrame WaitForEndOfFrameField => m_waitForEndOfFrame;
        public bool IsDirty { get => m_isDirty; set => m_isDirty = value; }

        public void RunOnEnable() => OnEnable();
        public void RunOnDisable() => OnDisable();
        public Element RunInstantiate() => Instantiate();
        public void RunBaseDestroy(Element objectToDestroy) => base.Destroy(objectToDestroy);
        public void RunSetupContent() => SetupContent();
        public void RunPositionChanged(Vector2 normalizedPosition) => PositionChanged(normalizedPosition);
        public void RunAdjustSizeToFitContents() => AdjustSizeToFitContents();
        public void RunMoveFromEndToStart(int delta) => MoveFromEndToStart(delta);
        public void RunMoveFromStartToEnd(int delta) => MoveFromStartToEnd(delta);
        public int RunIndexOfFirstEntry(float normalizedPosition) => IndexOfFirstEntry(normalizedPosition);
        public void RunAddNewObjectsToEnd(int amount) => AddNewObjectsToEnd(amount);
        public void RunAddNewObjectsToStart(int amount) => AddNewObjectsToStart(amount);
        public void RunInstantiateNewObject(int objectIndex, int entryIndex) => InstantiateNewObject(objectIndex, entryIndex);
        public void RunRemoveObjects(int amount) => RemoveObjects(amount);
        public float RunPositionFromEntryIndex(int entryIndex) => PositionFromEntryIndex(entryIndex);
        public bool RunSetupAtEndOfFrame() => SetupAtEndOfFrame();
        public IEnumerator RunCoroutineSetupAtEndOfFrame() => Coroutine_SetupAtEndOfFrame();
        public void RunMarkDirty() => MarkDirty();
        public static int RunIncrementLoopingIndex(int index, int length) => IncrementLoopingIndex(index, length);
        public static int RunDecrementLoopingIndex(int index, int length) => DecrementLoopingIndex(index, length);
        public static int RunMod(int n, int modulus) => Mod(n, modulus);
    }
}
