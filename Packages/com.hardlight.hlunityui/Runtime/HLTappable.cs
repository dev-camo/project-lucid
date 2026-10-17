// Original HLUnityUI.Runtime Hardlight.HLTappable (TypeDef 0x020000f8) and TappedEvent (0x020000f9),
// from the x86-64 bodies 0x1B73980..0x1B73A90 (methods 0x060006f9..0x060006fe).
// No shipped prefab, scene or asset references the exported script GUID c8f5642dfc008bc66e8d60dd1a2cb300.
using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class HLTappable : HLSelectable
    {
        [Serializable]
        public class TappedEvent : UnityEvent
        {
        }

        [SerializeField]
        protected TappedEvent m_onTap = new TappedEvent();
        [SerializeField]
        private float m_tapTimeThreshold = 0.1f;
        private float m_tapStartTime;

        public TappedEvent OnTap
        {
            get { return m_onTap; }
            set { m_onTap = value; }
        }

        // Neither pointer handler calls the HLSelectable base, and neither reads the event data,
        // so any button counts and the selection state is left alone.
        public override void OnPointerDown(PointerEventData eventData)
        {
            m_tapStartTime = Time.realtimeSinceStartup;
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            if (Time.realtimeSinceStartup - m_tapStartTime < m_tapTimeThreshold)
                m_onTap.Invoke();
        }
    }
}
