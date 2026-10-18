using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009f9; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CycleGameObjectList : MonoBehaviour
    {
        [SerializeField] private GameObject[] m_objects;
        private int m_currentIndex;

        // 0x0600394b
        private void OnEnable()
        {
            UpdateIndexActive();
        }

        // 0x0600394c
        public void Action_Next()
        {
            m_currentIndex++;
            UpdateIndexActive();
        }

        // 0x0600394d
        public void Action_Previous()
        {
            m_currentIndex--;
            UpdateIndexActive();
        }

        // 0x0600394e (0x6AF8E0): one modulo by the length, read once; an empty list faults on the idiv.
        private void UpdateIndexActive()
        {
            int count = m_objects.Length;
            m_currentIndex = (m_currentIndex + count) % count;
            for (int i = 0; i < count; ++i)
                m_objects[i].SetActive(i == m_currentIndex);
        }

        // 0x0600394f
        [ContextMenu("Assign From Children")]
        private void AssignFromChildren()
        {
            m_objects = new GameObject[transform.childCount];
            for (int i = 0; i < m_objects.Length; ++i)
                m_objects[i] = transform.GetChild(i).gameObject;
        }
    }
}
