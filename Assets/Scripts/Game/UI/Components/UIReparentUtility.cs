using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200084a; bodies translated from the 1.10.1 x86-64 binary.
    // Moves this UI element up the hierarchy and optionally stretches it to fill its new parent.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIReparentUtility : MonoBehaviour
    {
        [SerializeField] private int m_levelsToMoveUp;

        [Min(-1)]
        [Tooltip("Force child index of new parent, -1 leaves as last child")]
        [SerializeField] private int m_setChildIndex = -1;

        [SerializeField] private bool m_stretchWidth;
        [SerializeField] private bool m_stretchHeight;
        private RectTransform m_rectTransform;
        private Transform m_initialParent;

        // 0x06003004
        private void Awake()
        {
            m_rectTransform = transform as RectTransform;
        }

        // 0x06003005: re-parents without keeping the world position, resets the anchored position,
        // climbs up to m_levelsToMoveUp levels (stopping at the root), then applies the sibling
        // index and zeroes the stretched offset axes.
        public void ApplyReparenting()
        {
            m_initialParent = transform.parent;
            transform.SetParent(m_initialParent, false);
            m_rectTransform.anchoredPosition = Vector2.zero;
            for (int level = 0; level < m_levelsToMoveUp; level++)
            {
                Transform newParent = transform.parent.parent;
                if (newParent == null)
                    break;
                transform.parent = newParent;
            }
            if (m_setChildIndex != -1)
                transform.SetSiblingIndex(m_setChildIndex);
            m_rectTransform.offsetMin = new Vector2(
                m_stretchWidth ? 0f : m_rectTransform.offsetMin.x,
                m_stretchHeight ? 0f : m_rectTransform.offsetMin.y);
            m_rectTransform.offsetMax = new Vector2(
                m_stretchWidth ? 0f : m_rectTransform.offsetMax.x,
                m_stretchHeight ? 0f : m_rectTransform.offsetMax.y);
        }

        // 0x06003006: the child index defaults to -1.
        public UIReparentUtility() { }
    }
}
