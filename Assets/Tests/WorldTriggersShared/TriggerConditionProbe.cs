using HardlightProject;

namespace ProjectLucid.Tests.WorldTriggersShared
{
    // Test-only TriggerCondition. A case chooses what each virtual answers and reads how often
    // each ran; UseBase forwards DoEvaluate() to the shipped current-character lookup instead.
    // Editor-only assemblies cannot supply attachable components.
    public sealed class TriggerConditionProbe : TriggerCondition
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
