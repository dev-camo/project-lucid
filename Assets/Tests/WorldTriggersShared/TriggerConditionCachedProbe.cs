using HardlightProject;

namespace ProjectLucid.Tests.WorldTriggersShared
{
    // Test-only TriggerCondition_CachedBase asset. A case chooses what DoEvaluate(Character)
    // answers and reads how often it ran; UseBase forwards DoEvaluate() to the shipped
    // current-character lookup. Editor-only assemblies cannot supply instantiable assets.
    public sealed class TriggerConditionCachedProbe : TriggerCondition_CachedBase
    {
        public bool UseBase;
        public bool Result;
        public bool CharacterResult;
        public int Evaluations;
        public int CharacterEvaluations;
        public Character LastCharacter;

        protected override bool DoEvaluate()
        {
            Evaluations++;
            return UseBase ? base.DoEvaluate() : Result;
        }

        protected override bool DoEvaluate(Character character)
        {
            CharacterEvaluations++;
            LastCharacter = character;
            return CharacterResult;
        }
    }
}
