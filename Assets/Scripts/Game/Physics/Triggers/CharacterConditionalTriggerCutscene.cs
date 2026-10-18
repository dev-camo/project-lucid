using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 02000428. A conditional character trigger whose actions load or
    // play the first valid cutscene of a trigger type.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterConditionalTriggerCutscene : CharacterConditionalTriggerEvent
    {
        [SerializeField]
        [Tooltip("New trigger types can be added in CutsceneTriggerType asset. You should not need to use MissionCompleted/HubEntered/LevelEntered/LevelSelected in this component.")]
        private CutsceneTriggerType m_cutsceneTriggerType;
        private CutsceneManager m_cutsceneManager;

        // 06001874
        protected override void OnEnable()
        {
            base.OnEnable();
            ProcessManager.GetSystemRef<CutsceneManager>(null, true).InvokeOnValid(manager => m_cutsceneManager = manager);
        }

        // 06001875; four trigger types are already fired by the game's own FSM. The message keeps the
        // original stray dollar sign.
        private void OnValidate()
        {
            if (!gameObject.CanValidate())
                return;
            switch (m_cutsceneTriggerType)
            {
                case CutsceneTriggerType.OnMissionCompleted:
                case CutsceneTriggerType.OnLevelEntered:
                case CutsceneTriggerType.OnLevelSelected:
                case CutsceneTriggerType.OnResultsScreenClosed:
                    HLOutput.LogError("Cutscene trigger type $" + m_cutsceneTriggerType.ToString()
                        + " is already fired automatically from the FSM. Note you can add new types in CutsceneTriggerType asset.");
                    break;
            }
        }

        // 06001876
        public void Action_TriggerCutscene()
        {
            if (m_cutsceneManager.HasAnyValidCutscene(m_cutsceneTriggerType, out CutsceneDefinition cutscene))
                m_cutsceneManager.LoadCutscene(cutscene, null, true);
        }

        // 06001877
        public void Action_PreloadCutscene()
        {
            if (m_cutsceneManager.HasAnyValidCutscene(m_cutsceneTriggerType, out CutsceneDefinition cutscene))
                m_cutsceneManager.LoadCutscene(cutscene, null, false);
        }

        // 06001878
        public CharacterConditionalTriggerCutscene() { }
    }
}
