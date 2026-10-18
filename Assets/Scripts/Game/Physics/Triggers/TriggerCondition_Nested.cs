using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 02000755. Groups other conditions under an All/Any check.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class TriggerCondition_Nested : TriggerCondition
    {
        [SerializeField]
        private CharacterConditionalTriggerEvent.CheckType m_checkType;
        [SerializeField]
        private TriggerCondition[] m_conditions;

        // 06002922
        protected override bool DoEvaluate(Character character)
            => CharacterConditionalTriggerEvent.EvaluateConditions(character, m_conditions, m_checkType);

        // 06002923
        public TriggerCondition_Nested() { }
    }
}
