using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SurfaceEvent : MonoBehaviour
    {
        [SerializeField] private UnityEvent m_onEnter;
        [SerializeField] private UnityEvent m_onStay;
        [SerializeField] private UnityEvent m_onExit;
        private Coroutine m_stayCoroutine;
        private readonly WaitForFixedUpdate m_waitForFixedUpdate = new WaitForFixedUpdate();

        public void OnSurfaceEnter()
        {
            m_onEnter.Invoke();
            // Authored persistent listeners gate the routine. Runtime listeners alone
            // do not start it; re-entry keeps any earlier routine running.
            if (m_onStay.GetPersistentEventCount() > 0)
                m_stayCoroutine = StartCoroutine(ProcessStayEvents());
        }

        public void OnSurfaceExit()
        {
            if (m_stayCoroutine != null)
            {
                StopCoroutine(m_stayCoroutine);
                m_stayCoroutine = null;
            }
            m_onExit.Invoke();
        }

        private IEnumerator ProcessStayEvents()
        {
            // The first stay callback precedes the first fixed-update yield. Read
            // both fields again each iteration so callbacks may replace event data.
            while (true)
            {
                m_onStay.Invoke();
                yield return m_waitForFixedUpdate;
            }
        }
    }
}
