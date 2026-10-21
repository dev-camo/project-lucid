using System.Collections.Generic;
using Hardlight;

namespace ProjectLucid.Tests.UIContainerShared
{
    // Test-only UIContainer standing in for the per-mission results prefab a UIContainerMissionResults instantiates. It
    // records the parameters of each Setup call. Editor-only assemblies cannot supply attachable components.
    public sealed class MissionResultsRecordingContainer : UIContainer
    {
        public readonly List<IUIContainerParameters> SetupParameters = new List<IUIContainerParameters>();

        public override void Setup(IUIContainerParameters parameters) => SetupParameters.Add(parameters);
    }
}
