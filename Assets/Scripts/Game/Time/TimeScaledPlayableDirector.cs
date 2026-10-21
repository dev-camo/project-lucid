using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;

namespace HardlightProject
{
    // Original Game.Runtime TimeScaledPlayableDirector (TypeDef 0x0200080d, x86-64 0x5FD440..0x5FD77F): a
    // PlayableDirector stepped by hand, once per Update, by the scaled time of its category.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [RequireComponent(typeof(PlayableDirector))]
    public class TimeScaledPlayableDirector : MonoBehaviour, ITimeScaled
    {
        [SerializeField]
        [Tooltip("Playable Director will be manually updated to passed timescale category.\nNote, the play on awake behaviour will be controlled by this component.")]
        private PlayableDirector m_playableDirector;
        [SerializeField] private TimeCategoryObject m_timeCategory;
        [SerializeField] private bool m_playOnAwake;
        private readonly SystemRef<TimeManager> m_timeManagerRef = ProcessManager.GetSystemRef<TimeManager>();

        public bool IsPaused { get; set; } // 06002e8d / 06002e8e

        private void Reset() { m_playableDirector = GetComponent<PlayableDirector>(); } // 06002e8f

        private void Awake() // 06002e90
        {
            m_playableDirector.timeUpdateMode = DirectorUpdateMode.Manual;
            m_playableDirector.RebuildGraph();
            if (m_playOnAwake) Play();
        }

        // 06002e91: restarts from zero; the unsubscribe first keeps a replay from subscribing twice.
        public void Play()
        {
            m_timeManagerRef.Get().Unsubscribe(this, m_timeCategory);
            m_playableDirector.time = 0.0;
            m_timeManagerRef.Get().Subscribe(this, m_timeCategory);
        }

        // 06002e92: the time is advanced in double precision; reaching the asset's duration ends the playback.
        public void OnUpdate(float deltaTime)
        {
            m_playableDirector.time += deltaTime;
            if (m_playableDirector.time < m_playableDirector.playableAsset.duration)
            {
                m_playableDirector.Evaluate();
            }
            else
            {
                m_timeManagerRef.Get().Unsubscribe(this, m_timeCategory);
                m_playableDirector.time = 0.0;
            }
        }

        public void Stop() // 06002e93
        {
            m_timeManagerRef.Get().Unsubscribe(this, m_timeCategory);
            m_playableDirector.time = 0.0;
        }

        public TimeScaledPlayableDirector() { } // 06002e94
    }
}
