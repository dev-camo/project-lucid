using System.Collections.Generic;
using HardlightProject;

namespace ProjectLucid.Tests.UIContainerShared
{
    // Test-only dream power store that records what the widgets report instead of updating the displays: the original
    // OnDreamPowerChanged is virtual, so a derived container observes the callbacks the widget parameters carry.
    public sealed class DreamPowerStoreChangeRecordingContainer : UIContainerDreamPowerStore
    {
        public readonly List<(DreamPowerDefinition definition, UIDreamPowerStoreItemState itemState)> Changes =
            new List<(DreamPowerDefinition, UIDreamPowerStoreItemState)>();

        protected override void OnDreamPowerChanged(DreamPowerDefinition definition, UIDreamPowerStoreItemState itemState)
        {
            Changes.Add((definition, itemState));
        }
    }
}
