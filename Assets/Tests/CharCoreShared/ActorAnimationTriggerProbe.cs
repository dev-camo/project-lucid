using System;
using HardlightProject;

namespace ProjectLucid.Tests.CharCoreShared
{
    // Test-only closed ActorAnimationTrigger<Character> (an abstract generic MonoBehaviour) whose
    // filter is supplied by the case.
    public sealed class ActorAnimationTriggerProbe : ActorAnimationTrigger<Character>
    {
        public Func<Character, bool> Filter = _ => true;
        public int FilterCalls;

        protected override bool FilterActor(Character actor)
        {
            FilterCalls++;
            return Filter(actor);
        }
    }
}
