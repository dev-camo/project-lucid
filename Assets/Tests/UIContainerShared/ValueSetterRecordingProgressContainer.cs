using System.Collections.Generic;
using Hardlight;
using HardlightProject;

namespace ProjectLucid.Tests.UIContainerShared
{
    // Test-only UIContainer that reports progress, standing in for the container a UIContainerValueSetter
    // finds through its UIContainerIdentifier. Each IUIContainerProgress call is logged as written below.
    // Editor-only assemblies cannot supply attachable components.
    public sealed class ValueSetterRecordingProgressContainer : UIContainer, IUIContainerProgress
    {
        public readonly List<string> Calls = new List<string>();

        public override void SetEnabled(bool enabled) => Calls.Add("SetEnabled(" + enabled + ")");

        public void SetProgress(float progress) => Calls.Add("SetProgress(" + progress.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ")");

        public void SetProgress(int current, int total) => Calls.Add("SetProgress(" + current + "," + total + ")");
    }
}
