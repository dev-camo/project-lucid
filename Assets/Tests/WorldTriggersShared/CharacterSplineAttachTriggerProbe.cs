using Hardlight;
using HardlightProject;

namespace ProjectLucid.Tests.WorldTriggersShared
{
    // Test-only CharacterSplineAttachTrigger, which is abstract: the shipped descendants choose
    // the storage key, so this one names its own and counts how often it was asked. Editor-only
    // assemblies cannot supply attachable components.
    public sealed class CharacterSplineAttachTriggerProbe : CharacterSplineAttachTrigger
    {
        public static readonly GraphStorageKey Key = new GraphStorageKey("LucidSplineAttachProbe", 0, 0);
        public int KeyRequests;

        protected override GraphStorageKey GetFSMKey()
        {
            KeyRequests++;
            return Key;
        }
    }
}
