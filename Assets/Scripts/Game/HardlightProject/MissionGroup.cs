using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000704: complete authored mission-group model.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class MissionGroup
    {
        [SerializeField] private string m_name;
        [SerializeField, InspectorReadOnly] private string m_guid;
        [Tooltip("How many intros in this group should be played when the level starts.")]
        [SerializeField] private int m_introsToPlay = 3;
        [SerializeField] private List<MissionDefinition> m_definitions = new List<MissionDefinition>();
        [SerializeField] private List<RequirementMissionGroupBase> m_requirements;
        [SerializeField] private bool m_hideOrbCountUntilUnlocked = true;
        [Tooltip("Widget to instantiate when this mission group is unlocked.")]
        [SerializeField] private UIWidgetProgression m_progressionUnlockWidget;
        [SerializeField] private bool m_showAsGhostedWhenRequirementsNotMet;

        // Original 0x060026cd..0x060026d2: direct field reads, including the
        // IReadOnlyList return view of the original mutable List instance.
        public string GUID => m_guid;
        public int IntrosToPlay => m_introsToPlay;
        public IReadOnlyList<MissionDefinition> Definitions => m_definitions;
        public bool HideOrbCountUntilUnlocked => m_hideOrbCountUntilUnlocked;
        public UIWidgetProgression ProgressionUnlockWidget => m_progressionUnlockWidget;
        public bool ShowAsGhostedWhenRequirementsNotMet => m_showAsGhostedWhenRequirementsNotMet;

        // 0x060026d3: virtual requirement callbacks run in authored order and
        // stop at the first false result. An empty list succeeds. The original
        // does not replace absent lists or skip absent requirement instances.
        public bool MeetsAllRequirements(GameplayLevelDefinition levelDefinition)
        {
            foreach (RequirementMissionGroupBase requirement in m_requirements)
                if (!requirement.RequirementsMet(this, levelDefinition)) return false;
            return true;
        }

        // 0x060026d4: the resolved original generic method context names this
        // precise requirement class; it does not query the requirement result.
        public bool HasAlwaysHiddenRequirement()
        {
            return HasRequirementOfType<RequirementMissionGroupAlwaysHidden>();
        }

        // 0x060026d5: IsInst on both native backends accepts derived instances.
        public bool HasRequirementOfType<T>() where T : RequirementMissionGroupBase
        {
            foreach (RequirementMissionGroupBase requirement in m_requirements)
                if (requirement is T) return true;
            return false;
        }

        // 0x060026d6: original GUID-aware operators, direct definition first,
        // then its completed-mission override. This overload has no separate
        // override-null check; the original equality operator handles operands.
        public bool HasMission(MissionDefinition missionDefinition)
        {
            foreach (MissionDefinition definition in m_definitions)
                if (definition == missionDefinition ||
                    definition.CompletedMissionOverrides == missionDefinition) return true;
            return false;
        }

        // 0x060026d7: this overload compares GUID strings and explicitly tests
        // the override with ScriptableObjectWithGuid's inequality operator.
        public bool HasMission(string missionDefinitionGUID)
        {
            foreach (MissionDefinition definition in m_definitions)
                if (definition.GetGUID() == missionDefinitionGUID ||
                    (definition.CompletedMissionOverrides != null &&
                     definition.CompletedMissionOverrides.GetGUID() == missionDefinitionGUID)) return true;
            return false;
        }

        // 0x060026d8: the three initializers run before Object construction.
        // The authored requirements list remains null in a new instance.
        public MissionGroup() { }
    }
}
