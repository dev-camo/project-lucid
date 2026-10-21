using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

namespace HardlightProject
{
    // Original Game.Runtime TimelineHooks (TypeDef 0x02000a35, x86-64 0x6B8190..0x6B82DF): exposes a director's
    // stop as a UnityEvent and lets a timeline raise the started event.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class TimelineHooks : MonoBehaviour
    {
        [SerializeField] private PlayableDirector m_director;
        [SerializeField] private UnityEvent m_started;
        [SerializeField] private UnityEvent m_stopped;

        private void Start() { m_director.stopped += OnDirectorStopped; } // 06003a72
        private void OnDestroy() { m_director.stopped -= OnDirectorStopped; } // 06003a73

        public void Action_Started() // 06003a74
        {
            if (m_started != null) m_started.Invoke();
        }

        private void OnDirectorStopped(PlayableDirector _) // 06003a75
        {
            if (m_stopped != null) m_stopped.Invoke();
        }

        public TimelineHooks() { } // 06003a76
    }
}
