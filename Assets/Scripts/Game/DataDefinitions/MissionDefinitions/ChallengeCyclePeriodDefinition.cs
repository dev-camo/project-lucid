using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HardlightProject
{
    [UnityEngine.CreateAssetMenuAttribute(fileName = "ChallengeCyclePeriodDefinition", menuName = "HardlightProject/DefinitionData/Definitions/ChallengeCyclePeriodDefinition")]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute((Unity.IL2CPP.CompilerServices.Option)1, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute((Unity.IL2CPP.CompilerServices.Option)2, false)]
    // Original Game.Runtime 0x0200056f; complete owned fields and methods.
    public sealed class ChallengeCyclePeriodDefinition : ScriptableObject
    {
        [UnityEngine.SerializeField]
        // Original 0x04001373, native offset 0x18.
        private System.Collections.Generic.List<HardlightProject.ChallengeDefinition> m_challengeDefinitions;

        // Original 06001d5a returns the live authored List through its readonly interface.
        public IReadOnlyList<ChallengeDefinition> ChallengeDefinitions => m_challengeDefinitions;

        // Original 06001d5b only calls the real engine base; no List is allocated.
        public ChallengeCyclePeriodDefinition()
        {
        }
    }
}
