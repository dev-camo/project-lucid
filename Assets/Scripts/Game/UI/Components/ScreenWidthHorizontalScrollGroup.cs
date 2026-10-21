using System.Collections;
using System.Diagnostics;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200083d; bodies 0x609240..0x609740 (1.10.1 x86-64).
    // Lays its children out one canvas width apart and slides between them.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [ExecuteInEditMode]
    public class ScreenWidthHorizontalScrollGroup : MonoBehaviour
    {
        [SerializeField]
        private Canvas m_canvas;
        [SerializeField]
        private RectTransform[] m_childTransforms;
        [SerializeField]
        private float m_scrollSpeed;
        private RectTransform m_rectTransform;
        private int m_targetIndex;
        private Coroutine m_scrollRoutine;

        // 06002fbf: a plain cast, so a non-RectTransform transform throws.
        private void Awake()
        {
            m_rectTransform = (RectTransform)transform;
        }

        // 06002fc0
        private void OnEnable()
        {
            Refresh();
        }

        // 06002fc1: every child must be a RectTransform.
        private void Reset()
        {
            m_childTransforms = new RectTransform[transform.childCount];
            for (int i = 0; i < m_childTransforms.Length; i++)
            {
                m_childTransforms[i] = (RectTransform)transform.GetChild(i);
            }
        }

        // 06002fc2: the Conditional attribute only drops calls from other code; Unity still sends Update.
        [Conditional("UNITY_EDITOR")]
        private void Update()
        {
            Refresh();
        }

        // 06002fc3: child i sits at x = i * canvas width, keeping its own y.
        private void Refresh()
        {
            float canvasWidth = GetCanvasWidth();
            for (int i = 0; i < m_childTransforms.Length; i++)
            {
                Vector2 anchoredPosition = m_childTransforms[i].anchoredPosition;
                anchoredPosition.x = i * canvasWidth;
                m_childTransforms[i].anchoredPosition = anchoredPosition;
            }
        }

        // 06002fc4
        public void Action_ScrollToIndex(int index)
        {
            SelectTargetFromOffset(index - m_targetIndex);
        }

        // 06002fc5
        public void Action_ScrollToNext()
        {
            SelectTargetFromOffset(1);
        }

        // 06002fc6
        public void Action_ScrollToPrevious()
        {
            SelectTargetFromOffset(-1);
        }

        // 06002fc7: wraps around the children; the group itself moves the opposite way.
        // C# % keeps the dividend's sign, so a large negative offset can give a negative index.
        private void SelectTargetFromOffset(int indexOffset)
        {
            int childCount = m_childTransforms.Length;
            if (childCount == 0)
            {
                return;
            }
            float canvasWidth = GetCanvasWidth();
            float anchoredY = m_rectTransform.anchoredPosition.y;
            Vector2 fromPosition = new Vector2(m_targetIndex * -canvasWidth, anchoredY);
            m_targetIndex = (indexOffset + childCount + m_targetIndex) % childCount;
            Vector2 targetPosition = new Vector2(m_targetIndex * -canvasWidth, anchoredY);
            this.SafeStopCoroutine(ref m_scrollRoutine);
            m_scrollRoutine = StartCoroutine(ScrollToTargetPosition(fromPosition, targetPosition));
        }

        // 06002fc8 / MoveNext 06002fcd: the frame where the value first passes 1 is still lerped
        // (clamped) before the exact target is applied.
        private IEnumerator ScrollToTargetPosition(Vector2 fromPosition, Vector2 targetPosition)
        {
            float scrollLerpValue = 0f;
            while (scrollLerpValue <= 1f)
            {
                scrollLerpValue += (float)(Time.deltaTime * m_scrollSpeed);
                m_rectTransform.anchoredPosition = Vector2.Lerp(fromPosition, targetPosition, scrollLerpValue);
                yield return null;
            }
            m_rectTransform.anchoredPosition = targetPosition;
            m_scrollRoutine = null;
        }

        // 06002fc9
        private float GetCanvasWidth()
        {
            return ((RectTransform)m_canvas.transform).rect.width;
        }
    }
}
