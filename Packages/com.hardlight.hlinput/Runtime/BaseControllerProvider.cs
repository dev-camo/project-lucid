using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLInput.Runtime02000019: eight retained APIs, six fields, one genuine iterator owner.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class BaseControllerProvider : IBaseControllerProvider
    {
        private int m_numConnectedControllers;
        private readonly WaitForSeconds m_controllerConnectionPollingWait;
        private Coroutine m_controllerConnectionPollingCoroutine;
        protected bool m_activeControllerProvider;

        // Original06000051 is genuinely abstract; original06000052..53 are the direct backing-field accessors.
        public abstract IReadOnlyList<string> GetControllerNames();
        public FastAction OnControllerConnectionUpdate { get; set; }

        // Original retains private setter06000054 and this auto-property backing field; its getter was stripped.
        // Recompiled C# emits an extra private getter with no original token or reconstruction credit.
        private string LastAddedControllerName { get; set; }

        // Original06000055 allocates this exact WaitForSeconds after the Object base constructor.
        protected BaseControllerProvider(float controllerConnectionPollingRateInSeconds)
        {
            m_controllerConnectionPollingWait = new WaitForSeconds(controllerConnectionPollingRateInSeconds);
        }

        // Original06000056: replacing an existing handle does not stop its prior coroutine.
        public virtual void Initialise()
        {
            m_controllerConnectionPollingCoroutine = Hardlight.Utils.CoroutineUtils.RunCoroutine(ControllerConnectionPolling());
        }

        // Original06000057 passes the field by reference to the genuine original utility provider.
        public virtual void Shutdown()
        {
            Hardlight.Utils.CoroutineUtils.StopUtilCoroutine(ref m_controllerConnectionPollingCoroutine);
        }

        // Original06000058 and natural0200001a/06000059..5e: one retained yield state, no disposal cleanup.
        private IEnumerator ControllerConnectionPolling()
        {
            while (true)
            {
                IReadOnlyList<string> controllerNames = GetControllerNames();
                int count = controllerNames.Count;
                if (m_numConnectedControllers != count)
                {
                    m_numConnectedControllers = count;
                    LastAddedControllerName = count > 0 ? controllerNames[count - 1] : null;
                    OnControllerConnectionUpdate.Invoke();
                    // Both shipped architectures retain this empty loop and its live Count calls.
                    // A custom collection or a callback that mutates it can observe these calls/faults.
                    for (int i = 0; i < controllerNames.Count; ++i) { }
                }
                yield return m_controllerConnectionPollingWait;
            }
        }
    }
}
