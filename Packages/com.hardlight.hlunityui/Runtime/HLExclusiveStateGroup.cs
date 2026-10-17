using System;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityUI.Runtime 0x020000e2 (TypeDefIndex 12051); x86-64 bodies.
    // Elements are keyed by an explicit SetKeys value or, until then, by their
    // switcher's instance ID. Activation disables the non-matching elements in
    // order and enables only the last match (earlier matches are left as they
    // were); with no match it throws after the disables.
    public class HLExclusiveStateGroup : MonoBehaviour
    {
        // Original 0x020000e3 (TypeDefIndex 12050).
        [Serializable]
        private class StateGroupElement
        {
            public HLStateSwitcher StateSwitcher;
            private int m_key;
            private bool m_keysSet;

            public int Key
            {
                // 0x0600063a (0x1b6fce0)
                get
                {
                    if (m_keysSet)
                    {
                        return m_key;
                    }
                    return StateSwitcher.GetInstanceID();
                }
                // 0x0600063b (0x1b6fd60): the shipped setter body is empty.
                private set { }
            }

            // 0x0600063c (0x1b6fc30)
            public void SetKey(int key)
            {
                m_key = key;
                m_keysSet = true;
            }
        }

        [SerializeField]
        private StateGroupElement[] m_stateSwitchers;
        [SerializeField]
        private StateGroupElement m_defaultStateSwitcher;

        // 0x06000633 (0x1b6f870)
        public void Action_Activate(HLStateSwitcher stateSwitcher)
        {
            if (stateSwitcher != null)
            {
                ActivateStateGroupElement(stateSwitcher.GetInstanceID());
            }
        }

        // 0x06000634 (0x1b6f900)
        public void Action_Activate(int key)
        {
            ActivateStateGroupElement(key);
        }

        // 0x06000635 (0x1b6f8f0)
        public void Activate(int key)
        {
            ActivateStateGroupElement(key);
        }

        // 0x06000636 (0x1b6fa60): keys pair with elements by index. The default
        // element is a separate instance, so it takes the key of the element whose
        // switcher has the same instance ID. Each element without a key logs once;
        // surplus keys log once after the loop.
        public void SetKeys(int[] keys)
        {
            int i;
            for (i = 0; i < m_stateSwitchers.Length; i++)
            {
                if (i < keys.Length)
                {
                    if (m_stateSwitchers[i].StateSwitcher.GetInstanceID() == m_defaultStateSwitcher.StateSwitcher.GetInstanceID())
                    {
                        m_defaultStateSwitcher.SetKey(keys[i]);
                    }
                    m_stateSwitchers[i].SetKey(keys[i]);
                }
                else
                {
                    HLOutput.LogError("Not enough keys provided, more state switchers exist than keys");
                }
            }

            if (i < keys.Length)
            {
                HLOutput.LogError("Not enough state switchers provided, more keys exist than state switchers");
            }
        }

        // 0x06000637 (0x1b6fc40)
        private void OnEnable()
        {
            if (m_defaultStateSwitcher.StateSwitcher != null)
            {
                ActivateStateGroupElement(m_defaultStateSwitcher.Key);
            }
        }

        // 0x06000638 (0x1b6f910): disables every non-matching element in order,
        // then enables the last match. With no match the -1 index throws
        // IndexOutOfRangeException after the disables have run. targetIndex is the
        // shipped parameter name, but it holds a key, not an array index.
        private void ActivateStateGroupElement(int targetIndex)
        {
            int activeIndex = -1;
            for (int i = 0; i < m_stateSwitchers.Length; i++)
            {
                if (m_stateSwitchers[i].Key == targetIndex)
                {
                    activeIndex = i;
                }
                else
                {
                    m_stateSwitchers[i].StateSwitcher.Enabled = false;
                }
            }
            m_stateSwitchers[activeIndex].StateSwitcher.Enabled = true;
        }
    }
}
