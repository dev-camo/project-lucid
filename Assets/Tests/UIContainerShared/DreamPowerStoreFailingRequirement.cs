using HardlightProject;

namespace ProjectLucid.Tests.UIContainerShared
{
    // Test-only requirement that is never met: the dream power store is available only when its authored requirements hold,
    // and the original requirement assets are scene-specific.
    public sealed class DreamPowerStoreFailingRequirement : RequirementGameplayLevelBase
    {
        public override bool RequirementsMet(GameplayLevelDefinition levelDefinition) => false;
    }
}
