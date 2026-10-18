using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 02000ac2. Freezes the chosen time categories and
    // holds the character in place until the game is resumed.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterPause : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The time categories to be paused.")]
        private List<TimeCategory> m_timeCategories = new List<TimeCategory>();
        private SystemRef<CharacterManager> m_characterManagerRef;
        private TimeSetting m_pauseTimeSetting;
        private StackableDataHandle m_pauseTimeOverride;
        private Vector3 m_pausePosition;
        private Vector3 m_pauseVelocity;

        // 06003e0f; with no authored categories only System and UnityGlobal keep running.
        private void Start()
        {
            if (m_timeCategories != null && m_timeCategories.Count > 0)
            {
                m_pauseTimeSetting = TimeSetting.GetDefault(1f);
                foreach (TimeCategory category in m_timeCategories)
                    m_pauseTimeSetting.SetCategory(category, 0f);
            }
            else
            {
                m_pauseTimeSetting = TimeSetting.GetDefault(0f);
                m_pauseTimeSetting.SetCategory(TimeCategory.System, 1f);
                m_pauseTimeSetting.SetCategory(TimeCategory.UnityGlobal, 1f);
            }
            m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>(null, true);
        }

        // 06003e10; a pause already in force is replaced.
        public void PauseGame()
        {
            if (m_characterManagerRef.IsNull() ||
                !m_characterManagerRef.Get().TryGetCurrentCharacter(out Character character)) return;
            TimeManager timeManager = ProcessManager.GetSystem<TimeManager>(null, true);
            RemoveTimeOverride(timeManager);
            m_pauseTimeOverride = timeManager.ApplyTimeSetting(m_pauseTimeSetting);
            m_pausePosition = character.WorldPosition;
            m_pauseVelocity = character.WorldVelocity;
            character.MovementStop(true);
        }

        // 06003e11; the character only resumes when a pause was in force.
        public void ResumeGame()
        {
            if (!m_characterManagerRef.Get().TryGetCurrentCharacter(out Character character)) return;
            TimeManager timeManager = ProcessManager.GetSystem<TimeManager>(null, true);
            if (RemoveTimeOverride(timeManager))
                character.MovementResume(m_pausePosition, m_pauseVelocity);
        }

        // 06003e12
        private bool RemoveTimeOverride(TimeManager timeManager)
        {
            StackableDataHandle handle = m_pauseTimeOverride;
            if (handle != null)
            {
                timeManager.RemoveTimeSetting(handle);
                m_pauseTimeOverride = null;
            }
            return handle != null;
        }

        // 06003e13
        private void OnDisable()
        {
            TimeManager timeManager = ProcessManager.GetSystemSafe<TimeManager>(null, true);
            if (timeManager == null) return;
            RemoveTimeOverride(timeManager);
        }

        // 06003e14; the category list is initialised before MonoBehaviour's constructor.
        public CharacterPause() { }
    }
}
