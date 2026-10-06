using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "AchievementDefinition", menuName = "HardlightProject/DefinitionData/AchievementDefinition", order = 0)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    public class AchievementDefinition : UnityEngine.ScriptableObject
    {
        [UnityEngine.SerializeField]
        private HardlightProject.AchievementIdentifier m_achievementId;

        [UnityEngine.SerializeField]
        [Hardlight.Utils.HashEnum(null)]
        private Hardlight.Enums.Strings m_title;

        [UnityEngine.SerializeField]
        [Hardlight.Utils.HashEnum(null)]
        private Hardlight.Enums.Strings m_description;

        // Original Game.Runtime 0x0600195c, ARM 0x77fe00.
        public HardlightProject.AchievementIdentifier AchievementId => m_achievementId;

        // Original Game.Runtime 0x0600195d, ARM 0x77fe08.
        public Hardlight.Enums.Strings Title => m_title;

        // Original Game.Runtime 0x0600195e, ARM 0x77fe10.
        public Hardlight.Enums.Strings Description => m_description;

        // Original Game.Runtime 0x0600195f, ARM 0x77fe18.
        // The compiler emits the original public, parameterless base-only constructor.
    }
}
