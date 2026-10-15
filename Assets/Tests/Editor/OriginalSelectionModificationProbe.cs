using System;
using System.Collections.Generic;
using Hardlight;
using Source = HLButtonHighlightCustomisations.ButtonHighlightChangeSource;

namespace ProjectLucid.Editor
{
    // Test-only ModifiedOnUISelection that records each dispatched callback;
    // it provides no game API and is only attached to owned inactive objects.
    public sealed class OriginalSelectionModificationProbe : ModifiedOnUISelection
    {
        public sealed class Call
        {
            public OriginalSelectionModificationProbe Receiver;
            public string Method;
            public Source Previous, Current;
            public float Lerp = float.NaN;
        }

        public List<Call> Calls;
        public Action<OriginalSelectionModificationProbe> OnCall;

        public override void OnHighlightBegin(Source previousSource, Source currentSource) { Record("OnHighlightBegin", previousSource, currentSource, float.NaN); }
        public override void OnHighlightUpdated(Source previousSource, Source currentSource, float lerpValue) { Record("OnHighlightUpdated", previousSource, currentSource, lerpValue); }
        public override void OnHighlightEnd(Source previousSource, Source currentSource) { Record("OnHighlightEnd", previousSource, currentSource, float.NaN); }
        public override void OnActivate(Source source) { Record("OnActivate", null, source, float.NaN); }
        public override void OnDeactivate(Source source) { Record("OnDeactivate", null, source, float.NaN); }

        private void Record(string method, Source previous, Source current, float lerp)
        {
            Calls.Add(new Call { Receiver = this, Method = method, Previous = previous, Current = current, Lerp = lerp });
            if (OnCall != null) OnCall(this);
        }
    }
}
