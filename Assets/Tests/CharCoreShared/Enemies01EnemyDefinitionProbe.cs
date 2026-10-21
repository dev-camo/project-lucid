using HardlightProject;

namespace ProjectLucid.Tests.CharCoreShared
{
    // Test-only EnemyDefinition. The shipped Awake refreshes caches from authored settings that a
    // freshly created asset does not have, so the probe skips it and the case supplies Traits itself.
    public sealed class Enemies01EnemyDefinitionProbe : EnemyDefinition
    {
        protected override void Awake() { }
    }
}
