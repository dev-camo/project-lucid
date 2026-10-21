using System;
using HardlightProject;

namespace ProjectLucid.Tests.CharCoreShared
{
    // Test-only concrete leaf of the abstract rails homing definition: the original declares it abstract, so its
    // constructor initialisers and cached values are read through this derived definition. It is also the definition
    // type the targeting homing probe ability is closed over.
    public sealed class RailsHomingDefinitionProbe : CharacterAbilityDefinition_RailsHoming
    {
        public override Type ScriptType => typeof(RailsHomingDefinitionProbe);
    }
}
