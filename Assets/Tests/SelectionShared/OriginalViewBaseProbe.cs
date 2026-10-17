using System;
using System.Collections.Generic;
using Hardlight;

namespace ProjectLucid.Tests
{
    // Records Hardlight.ViewBase's abstract slot 4 (OnSetup, 0x060002b3) together with
    // the ViewParameters field as the subclass sees it during the call.
    // Editor-only assemblies cannot supply attachable components, so it lives in this runtime test assembly.
    public sealed class OriginalViewBaseProbe : ViewBase
    {
        [NonSerialized] public readonly List<string> Calls = new List<string>();
        [NonSerialized] public ViewParameters ReceivedParameters;
        [NonSerialized] public ViewParameters FieldDuringSetup;

        protected override void OnSetup(ViewParameters viewParams)
        {
            Calls.Add("OnSetup");
            ReceivedParameters = viewParams;
            FieldDuringSetup = ViewParameters;
        }
    }
}
