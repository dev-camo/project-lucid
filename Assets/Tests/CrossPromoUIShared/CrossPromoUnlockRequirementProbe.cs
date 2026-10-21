using System.Collections.Generic;
using HardlightProject;

namespace ProjectLucid.Tests.CrossPromoUIShared
{
    // Test-only requirement whose answer is set by the fixture. It records that it was asked, the level it was
    // asked about and, in a log the fixture shares between requirements, its label, so a case can see the
    // order and the short circuit of Requirements.AreMet.
    public sealed class CrossPromoUnlockRequirementProbe : RequirementGameplayLevelBase
    {
        public bool Met;
        public string Label;
        public List<string> Log;
        public bool Asked;
        public GameplayLevelDefinition Received;

        public override bool RequirementsMet(GameplayLevelDefinition levelDefinition)
        {
            Asked = true;
            Received = levelDefinition;
            if (Log != null) Log.Add(Label);
            return Met;
        }
    }
}
