using System.Collections;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 0x020008a1; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIContainerCredits : UIContainer
    {
        [SerializeField] private RectTransform m_creditsScrollParent;
        [SerializeField] private RectTransform m_endYPoint;
        [SerializeField] private RectTransform m_scrollToPoint;
        [SerializeField] private float m_scrollSpeed = 1f;
        [SerializeField] private float m_endWaitSeconds;
        [SerializeField] private UnityEvent m_onEndTrigger;

        // 0x060031de (0x629F50): scrolls the credits up until the scroll-to point has reached the end point's height,
        // waits, then fires the end trigger.
        private IEnumerator Start()
        {
            while (m_scrollToPoint.position.y < m_endYPoint.position.y)
            {
                m_creditsScrollParent.Translate(0f, Time.deltaTime * m_scrollSpeed, 0f);
                yield return null;
            }

            yield return new WaitForSeconds(m_endWaitSeconds);
            if (m_onEndTrigger != null)
                m_onEndTrigger.Invoke();
        }

        // 0x060031df (0x629FE0)
        public UIContainerCredits()
        {
        }
    }
}
