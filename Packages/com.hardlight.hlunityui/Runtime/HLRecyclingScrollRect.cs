// Original HLUnityUI.Runtime Hardlight.HLRecyclingScrollRect`2 020000e8 and its iterator
// <Coroutine_SetupAtEndOfFrame>d__53 020000e9, reconstructed from the fully shared
// x86-64 bodies (0x1393270..0x1394A40, 0xB0E230..0xB0E3A0), their RGCTX tables and
// metadata. Member order fixes the generated names: the 18 fields are ordinals 0..17
// and the five abstract properties with their accessors 18..28, so OnEnable is 35,
// OnDisable 36 and Coroutine_SetupAtEndOfFrame 53. Original local names are inferred.
// No original type derives from it; the exported script has no asset references.
//
// The objects are used as a ring: ReloadAllData gives
// m_objects[(m_indexOfFirstObject + i) % m_objectCount] the entry
// (m_indexOfFirstEntry + i) % m_dataCount.
using System.Collections;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class HLRecyclingScrollRect<T1, T2> : UIBehaviour where T1 : HLRecyclingScrollRectElement<T2>
    {
        [SerializeField]
        protected T1 m_listEntryPrefab;
        [SerializeField]
        protected HLScrollRect m_scrollRect;
        [SerializeField]
        [Tooltip("List Elements will be added inside this object")]
        protected RectTransform m_content;
        [SerializeField]
        [Tooltip("Total padding at the beginning and end of the Content Object, within the Scroll Rect Content object")]
        protected Vector2 m_externalPadding = Vector2.zero;
        [Tooltip("Padding within the Content object")]
        [SerializeField]
        protected RectOffset m_internalPadding = new RectOffset();
        [SerializeField]
        protected float m_spacing;
        [Tooltip("When this option is selected, if the scroll rect is resized, new children will be added/removed as required")]
        [SerializeField]
        protected bool m_regenerateOnSizeChanged;
        [SerializeField]
        protected float m_allowance = 20f;
        protected readonly List<T1> m_objects = new List<T1>();
        protected IReadOnlyList<T2> m_data = new List<T2>();
        protected int m_indexOfFirstEntry;
        protected int m_indexOfFirstObject = -1;
        protected int m_objectCount;
        protected int m_dataCount;
        protected int m_resizeOffset = 1;
        protected int m_maxObjectsRequired;
        // Never yielded: the setup coroutine waits with two null yields instead.
        protected WaitForEndOfFrame m_waitForEndOfFrame = new WaitForEndOfFrame();
        protected bool m_isDirty;

        protected abstract float StartPadding { get; }
        protected abstract float EndPadding { get; }
        protected abstract float ContentSize { get; set; }
        protected abstract float ViewportSize { get; }
        protected abstract float ElementSize { get; }

        protected abstract float GetNormalizedPositionOnAxis(Vector2 normalizedPosition);
        protected abstract void SetChildPosition(ref T1 child, float position);

        // 0x0600065b (0x1393270): removing objects always resets the scroll, whatever
        // resetScroll says (the remove path falls through into ResetScroll at 0x1393370).
        public void SetEntries(IReadOnlyList<T2> entryData, bool resetScroll = true)
        {
            m_data = entryData;
            m_dataCount = m_data.Count;
            AdjustSizeToFitContents();
            int delta = Mathf.Min(m_dataCount, m_maxObjectsRequired) - m_objectCount;
            if (delta < 0)
            {
                RemoveObjects(-delta);
                resetScroll = true;
            }
            else if (delta > 0)
            {
                AddNewObjectsToEnd(delta);
            }
            if (resetScroll)
                m_scrollRect.ResetScroll();
            RefreshView();
            ReloadAllData();
        }

        public void RefreshView()
        {
            PositionChanged(m_scrollRect.normalizedPosition);
        }

        // 0x0600065d: the entry is read before the object it is given to.
        public void ReloadAllData()
        {
            for (int i = 0; i < m_objectCount; i++)
            {
                T2 data = m_data[(m_indexOfFirstEntry + i) % m_dataCount];
                m_objects[(m_indexOfFirstObject + i) % m_objectCount].Initialise(data);
            }
        }

        // 0x0600065e: objects are destroyed last to first and the indices return to their
        // constructor values; m_dataCount is cleared while m_data is kept.
        public void Clear()
        {
            for (int i = m_objectCount - 1; i >= 0; i--)
                Destroy(m_objects[i]);
            m_objects.Clear();
            m_indexOfFirstEntry = 0;
            m_indexOfFirstObject = -1;
            m_objectCount = 0;
            m_dataCount = 0;
        }

        // 0x0600065f / 0x06000660: each callback builds its own lambda, so the listener
        // added here is never the one RemoveListener is given and stays registered.
        protected override void OnEnable()
        {
            base.OnEnable();
            SetupAtEndOfFrame();
            m_scrollRect.onValueChanged.AddListener(normalizedPosition => PositionChanged(normalizedPosition));
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            m_scrollRect.onValueChanged.RemoveListener(normalizedPosition => PositionChanged(normalizedPosition));
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (m_regenerateOnSizeChanged)
                MarkDirty();
        }

        protected virtual T1 Instantiate()
        {
            return Object.Instantiate(m_listEntryPrefab, m_content);
        }

        protected virtual void Destroy(T1 objectToDestroy)
        {
            Object.Destroy(objectToDestroy.gameObject);
        }

        // 0x06000664: m_dataCount is read before the virtual size getters run.
        protected void SetupContent()
        {
            int dataCount = m_dataCount;
            m_maxObjectsRequired = Mathf.FloorToInt((ViewportSize + 2f * m_allowance) / (ElementSize + m_spacing)) + 2;
            int delta = Mathf.Min(m_maxObjectsRequired, dataCount) - m_objectCount;
            if (delta < 0)
            {
                RemoveObjects(-delta);
            }
            else if (delta > 0)
            {
                int amountAtEnd = Mathf.Min(delta, dataCount - m_indexOfFirstEntry + 1);
                AddNewObjectsToEnd(amountAtEnd);
                AddNewObjectsToStart(delta - amountAtEnd);
            }
            ContentReady();
        }

        protected virtual void ContentReady()
        {
        }

        protected void PositionChanged(Vector2 normalizedPosition)
        {
            if (m_dataCount == 0)
                return;
            int delta = IndexOfFirstEntry(GetNormalizedPositionOnAxis(normalizedPosition)) - m_indexOfFirstEntry;
            if (delta > 0)
                MoveFromStartToEnd(Mathf.Min(delta, m_dataCount - (m_indexOfFirstEntry + m_objectCount)));
            else if (delta < 0)
                MoveFromEndToStart(-delta);
            for (int i = 0; i < m_objects.Count; i++)
                m_objects[i].OnScroll();
        }

        // 0x06000667: the counts are read before the virtual getters run.
        protected void AdjustSizeToFitContents()
        {
            int count = m_dataCount + m_resizeOffset;
            ContentSize = StartPadding + EndPadding + ElementSize * count + m_spacing * Mathf.Max(0, count - 1);
        }

        // 0x06000668: the first entry moves back by the whole delta while at most
        // m_objectCount objects are reassigned.
        protected void MoveFromEndToStart(int delta)
        {
            int amount = Mathf.Min(delta, m_objectCount);
            m_indexOfFirstEntry -= delta;
            m_indexOfFirstObject = Mod(m_indexOfFirstObject - delta, m_objectCount);
            int objectIndex = m_indexOfFirstObject;
            int entryIndex = m_indexOfFirstEntry;
            for (int i = 0; i < amount; i++)
            {
                T1 child = m_objects[objectIndex];
                child.Initialise(m_data[entryIndex]);
                SetChildPosition(ref child, PositionFromEntryIndex(entryIndex));
                entryIndex++;
                objectIndex = IncrementLoopingIndex(objectIndex, m_objectCount);
            }
        }

        // 0x06000669: unlike MoveFromEndToStart, nothing (not even the indices) changes
        // unless at least one object moves.
        protected void MoveFromStartToEnd(int delta)
        {
            int amount = Mathf.Min(delta, m_objectCount);
            if (amount <= 0)
                return;
            int entryIndex;
            int objectIndex;
            if (delta < m_objectCount)
            {
                entryIndex = m_indexOfFirstEntry + m_objectCount;
                objectIndex = m_indexOfFirstObject;
            }
            else
            {
                entryIndex = m_indexOfFirstEntry + delta;
                objectIndex = (m_indexOfFirstObject + delta) % m_objectCount;
            }
            for (int i = 0; i < amount; i++)
            {
                T1 child = m_objects[objectIndex];
                child.Initialise(m_data[entryIndex]);
                SetChildPosition(ref child, PositionFromEntryIndex(entryIndex));
                entryIndex++;
                objectIndex = IncrementLoopingIndex(objectIndex, m_objectCount);
            }
            m_indexOfFirstEntry += delta;
            m_indexOfFirstObject = (m_indexOfFirstObject + delta) % m_objectCount;
        }

        // 0x0600066a: the start offset counts StartPadding, the leading external padding,
        // the allowance and one whole element.
        protected int IndexOfFirstEntry(float normalizedPosition)
        {
            float scrolled = Mathf.Max(0f, ContentSize - ViewportSize) * (1f - normalizedPosition);
            float offset = scrolled - (StartPadding + m_externalPadding.x + m_allowance + ElementSize);
            return Mathf.Clamp(Mathf.CeilToInt(offset / (ElementSize + m_spacing)), 0, m_dataCount - 1);
        }

        // 0x0600066b: new objects are inserted at the first-object slot, which is the
        // logical end of the ring; a negative first index (the constructor's -1) restarts
        // the ring at 0.
        protected void AddNewObjectsToEnd(int amount)
        {
            int objectIndex = Mathf.Max(0, m_indexOfFirstObject);
            int entryIndex = m_indexOfFirstEntry + m_objectCount;
            for (int i = 0; i < amount; i++)
                InstantiateNewObject(objectIndex + i, entryIndex + i);
            m_indexOfFirstObject = m_indexOfFirstObject < 0 ? 0 : m_indexOfFirstObject + amount;
            m_objectCount = m_objects.Count;
        }

        // 0x0600066c: each new object is inserted at the first-object slot for the entry
        // before the previous one; m_indexOfFirstEntry itself is not moved.
        protected void AddNewObjectsToStart(int amount)
        {
            int entryIndex = m_indexOfFirstEntry - 1;
            for (int i = 0; i < amount; i++)
                InstantiateNewObject(m_indexOfFirstObject, entryIndex - i);
            m_objectCount = m_objects.Count;
        }

        protected void InstantiateNewObject(int objectIndex, int entryIndex)
        {
            T1 newObject = Instantiate();
            newObject.name = newObject.name.Replace("(Clone)", objectIndex.ToString());
            m_objects.Insert(objectIndex, newObject);
            newObject.Initialise(m_data[entryIndex]);
            SetChildPosition(ref newObject, PositionFromEntryIndex(entryIndex));
        }

        // 0x0600066e: objects are taken from just before the first object (the end of
        // the ring) and then from the end of the list; both ranges are destroyed before
        // either is removed, the end range first.
        protected void RemoveObjects(int amount)
        {
            int indexOfFirstObject = m_indexOfFirstObject;
            int amountBeforeFirst = Mathf.Min(amount, indexOfFirstObject);
            int objectCount = m_objectCount;
            int amountAtEnd = amount - amountBeforeFirst;
            int startOfFirstRange = indexOfFirstObject - amountBeforeFirst;
            for (int i = 0; i < amountBeforeFirst; i++)
                Destroy(m_objects[startOfFirstRange + i]);
            int startOfEndRange = objectCount - amountAtEnd;
            for (int i = 0; i < amountAtEnd; i++)
                Destroy(m_objects[startOfEndRange + i]);
            m_objects.RemoveRange(startOfEndRange, amountAtEnd);
            m_objects.RemoveRange(startOfFirstRange, amountBeforeFirst);
            m_indexOfFirstObject = startOfFirstRange;
            m_objectCount = m_objects.Count;
        }

        protected float PositionFromEntryIndex(int entryIndex)
        {
            return StartPadding + (ElementSize + m_spacing) * entryIndex;
        }

        // 0x06000670: the coroutine is only started in an active hierarchy.
        protected bool SetupAtEndOfFrame()
        {
            if (!gameObject.activeInHierarchy)
                return false;
            StartCoroutine(Coroutine_SetupAtEndOfFrame());
            return true;
        }

        // 0x0600067b: resize, yield null twice, then build the objects.
        protected IEnumerator Coroutine_SetupAtEndOfFrame()
        {
            AdjustSizeToFitContents();
            yield return null;
            yield return null;
            SetupContent();
            RefreshView();
            m_isDirty = false;
        }

        protected void MarkDirty()
        {
            if (m_isDirty)
                return;
            if (SetupAtEndOfFrame())
                m_isDirty = true;
        }

        protected static int IncrementLoopingIndex(int index, int length)
        {
            return (index + 1) % length;
        }

        // 0x06000674 (0x1394830): no modulo, so only index 0 wraps.
        protected static int DecrementLoopingIndex(int index, int length)
        {
            return (index == 0 ? length : index) - 1;
        }

        // Non-negative remainder for a positive modulus.
        protected static int Mod(int n, int modulus)
        {
            int remainder = n % modulus;
            return remainder < 0 ? remainder + modulus : remainder;
        }
    }
}
