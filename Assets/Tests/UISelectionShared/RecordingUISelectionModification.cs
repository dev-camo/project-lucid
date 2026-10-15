using System.Collections.Generic;
using Hardlight;
using Source = HLButtonHighlightCustomisations.ButtonHighlightChangeSource;

namespace ProjectLucid.Tests.UISelectionShared
{
    // Test-only concrete host for the abstract original ModifiedOnUISelection.
    // Editor-only assemblies cannot supply attachable components. Each callback
    // appends to a caller-owned list so callers can assert cross-layer order.
    public sealed class RecordingUISelectionModification : ModifiedOnUISelection
    {
        public sealed class Call
        {
            public RecordingUISelectionModification Owner;
            public string Name;
            public Source Previous;
            public Source Current;
            public float Lerp;
        }

        public List<Call> Sink;

        private void Record(string name, Source previous, Source current, float lerp = float.NaN)
        {
            Sink.Add(new Call { Owner = this, Name = name, Previous = previous, Current = current, Lerp = lerp });
        }

        public override void OnHighlightBegin(Source previousSource, Source currentSource) => Record("Begin", previousSource, currentSource);
        public override void OnHighlightUpdated(Source previousSource, Source currentSource, float lerpValue) => Record("Updated", previousSource, currentSource, lerpValue);
        public override void OnHighlightEnd(Source previousSource, Source currentSource) => Record("End", previousSource, currentSource);
        public override void OnActivate(Source source) => Record("Activate", null, source);
        public override void OnDeactivate(Source source) => Record("Deactivate", null, source);
    }
}
